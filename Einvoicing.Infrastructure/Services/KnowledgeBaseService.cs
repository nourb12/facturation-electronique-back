using System.IO.Compression;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Einvoicing.Infrastructure.Services;

public sealed class KnowledgeBaseService(
    ContextBaseDeDonnees db,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : IKnowledgeBaseService
{
    private const string SupportAnswer = "Pour obtenir une réponse précise, contactez l’assistance TuniFlow : support@tuniflow.tn.";
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Word = new(@"[\p{L}\p{N}]{3,}", RegexOptions.Compiled);
    private static readonly Regex Sentence = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
    private static readonly string[] NoiseMarkers = [
        "chapitres indexables", "questions auxquelles", "table des matieres", "table des matières",
        "sprint", "backlog", "| ---", "resume et introduction", "résumé et introduction",
        "regles d usage dans le rag", "règles d'usage dans le rag", "afficher comme source",
        "ne pas l utiliser comme source", "ne pas l'utiliser comme source", "document academique",
        "document académique", "source de droit fiscal", "donnees sensibles", "données sensibles"
    ];
    private static readonly HashSet<string> SearchStopWords = [
        "comment", "commen", "quel", "quelle", "quels", "quelles", "est", "sont", "une", "un",
        "des", "les", "le", "la", "dans", "pour", "avec", "sur", "que", "qui", "quoi", "faire",
        "fonctionne", "fonctionner", "peux", "puis", "je", "tu", "vous", "mon", "ma", "mes"
    ];
    private static readonly string[] ProcedureVerbs = [
        "creer", "cree", "ajouter", "enregistrer", "modifier", "supprimer", "importer",
        "televerser", "telecharger", "valider", "envoyer", "declarer", "payer", "relancer"
    ];

    public async Task<IReadOnlyList<KnowledgeDocumentDto>> ListAsync(CancellationToken ct = default)
        => await db.KnowledgeDocuments.AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => ToDto(d, d.Chunks.Count))
            .ToListAsync(ct);

    public async Task<KnowledgeDocumentDto> ImportAsync(
        Guid userId,
        string title,
        string category,
        string source,
        DateTime? publishedAt,
        Stream content,
        string fileName,
        string mimeType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Le titre est obligatoire.");
        if (content.CanSeek && content.Length == 0) throw new ArgumentException("Le document est vide.");
        if (content.CanSeek && content.Length > 15 * 1024 * 1024) throw new ArgumentException("Le document ne doit pas dépasser 15 Mo.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".md" and not ".txt" and not ".docx")
            throw new ArgumentException("Formats pris en charge : Markdown, texte et Word (.docx). Les PDF scannés passent d'abord par le module OCR.");

        var root = configuration["Uploads:Root"];
        root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root);
        var folder = Path.Combine(root, "knowledge-base");
        Directory.CreateDirectory(folder);

        var storageName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folder, storageName);
        await using (var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
            await content.CopyToAsync(target, ct);

        string text;
        string? extractionError = null;
        try
        {
            text = await ExtractTextAsync(absolutePath, extension, ct);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Aucun texte exploitable n'a été extrait.");
        }
        catch (Exception exception)
        {
            text = string.Empty;
            extractionError = exception.Message;
        }

        var document = new KnowledgeDocument
        {
            CreatedById = userId,
            Title = title.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Guide produit" : category.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? "Interne" : source.Trim(),
            Status = "Brouillon",
            OriginalFileName = Path.GetFileName(fileName),
            StoragePath = Path.Combine("uploads", "knowledge-base", storageName).Replace("\\", "/"),
            MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
            PublishedAt = publishedAt,
            ExtractedText = text,
            ExtractionError = extractionError
        };

        if (extractionError is null)
            document.Chunks = SplitIntoChunks(text).ToList();

        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return ToDto(document, document.Chunks.Count);
    }

    public async Task SetStatusAsync(Guid id, string status, Guid userId, CancellationToken ct = default)
    {
        var normalized = status.Trim().ToLowerInvariant() switch
        {
            "valide" => "Valide",
            "archive" => "Archive",
            _ => "Brouillon"
        };
        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Document introuvable.");
        document.Status = normalized;
        document.ValidatedAt = normalized == "Valide" ? DateTime.UtcNow : null;
        document.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Document introuvable.");
        db.KnowledgeDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeSearchResultDto>> SearchAsync(string query, int take = 5, CancellationToken ct = default)
    {
        var terms = ExpandTerms(query);
        if (terms.Length == 0) return [];
        var evidenceTerms = Word.Matches(Normalize(query))
            .Select(match => match.Value)
            .Where(term => !SearchStopWords.Contains(term))
            .SelectMany(term => ExpandTerms(term))
            .Where(term => !SearchStopWords.Contains(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var chunks = await db.KnowledgeChunks.AsNoTracking()
            .Include(c => c.Document)
            .Where(c => c.Document!.Status == "Valide")
            .ToListAsync(ct);

        return chunks
            .Where(chunk => IsUsefulChunk(chunk.Content))
            .Where(chunk => HasRelevantEvidence(chunk, evidenceTerms))
            .Select(chunk => new { Chunk = chunk, Score = Score(chunk, terms, Normalize(query)) })
            .Where(row => row.Score > 0)
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Chunk.Position)
            .Take(Math.Clamp(take, 1, 10))
            .Select(row => new KnowledgeSearchResultDto(
                row.Chunk.DocumentId,
                row.Chunk.Document!.Title,
                row.Chunk.Document.Category,
                row.Chunk.Document.Source,
                row.Chunk.Position,
                row.Chunk.Heading,
                row.Chunk.Content,
                row.Score))
            .ToArray();
    }

    public async Task<AssistantAnswerDto> AskAsync(string question, CancellationToken ct = default)
    {
        if (IsSalesVatQuestion(question))
            return new AssistantAnswerDto(
                "Pour consulter les ventes avec TVA, ouvrez Ventes puis Factures et sélectionnez la facture concernée. Le taux de TVA est affiché sur chaque ligne, ainsi que le détail et le total de TVA. Le filtre global « TVA applicable » n’est pas encore disponible dans la liste des factures.",
                true,
                "Aide produit",
                []);

        var results = await SearchAsync(question, 4, ct);
        if (!HasSufficientEvidence(question, results))
            return new AssistantAnswerDto(
                SupportAnswer,
                false,
                "Recherche locale",
                []);

        var isHowTo = IsProcedureQuestion(question);
        var selectedResults = isHowTo ? results.Take(1).ToArray() : results.Take(2).ToArray();
        var sources = selectedResults
            .GroupBy(r => r.DocumentId)
            .Select(group => new AssistantSourceDto(group.Key, group.First().DocumentTitle, group.First().Category, group.First().Source, group.First().Heading))
            .ToArray();
        var extracts = selectedResults.Select(r => $"{r.Heading}: {r.Content}").ToArray();
        var fallback = BuildReadableFallback(question, selectedResults);
        if (IsCashForecastCalculationQuestion(question))
            return new AssistantAnswerDto(fallback, true, "Recherche locale sourcée", sources);

        var generated = await GenerateWithOllamaAsync(question, extracts, ct);
        if (string.Equals(Normalize(generated ?? string.Empty), Normalize(SupportAnswer), StringComparison.Ordinal))
            return new AssistantAnswerDto(SupportAnswer, false, "Recherche locale", []);

        return new AssistantAnswerDto(generated ?? fallback, true, generated is null ? "Recherche locale sourcée" : "RAG local (Ollama)", sources);
    }

    private static KnowledgeDocumentDto ToDto(KnowledgeDocument document, int chunkCount)
        => new(document.Id, document.Title, document.Category, document.Source, document.Status,
            document.OriginalFileName, document.PublishedAt, document.ValidatedAt, chunkCount,
            document.ExtractionError, document.CreatedAt);

    private static async Task<string> ExtractTextAsync(string path, string extension, CancellationToken ct)
    {
        if (extension is ".md" or ".txt") return await File.ReadAllTextAsync(path, ct);

        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidOperationException("Document Word invalide.");
        await using var stream = entry.Open();
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join("\n", xml.Descendants(word + "p")
            .Select(p => string.Concat(p.Descendants(word + "t").Select(t => t.Value)).Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static IEnumerable<KnowledgeChunk> SplitIntoChunks(string text)
    {
        var heading = "Document";
        var buffer = new StringBuilder();
        var position = 0;

        foreach (var rawLine in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                if (buffer.Length > 0)
                {
                    yield return new KnowledgeChunk { Position = position++, Heading = heading, Content = buffer.ToString().Trim() };
                    buffer.Clear();
                }
                heading = line.TrimStart('#', ' ').Trim();
                continue;
            }
            if (line.Length == 0) continue;
            if (buffer.Length + line.Length > 1000)
            {
                yield return new KnowledgeChunk { Position = position++, Heading = heading, Content = buffer.ToString().Trim() };
                buffer.Clear();
            }
            if (buffer.Length > 0) buffer.Append(' ');
            buffer.Append(line);
        }
        if (buffer.Length > 0) yield return new KnowledgeChunk { Position = position, Heading = heading, Content = buffer.ToString().Trim() };
    }

    private static string[] ExpandTerms(string input)
    {
        var terms = Word.Matches(Normalize(input)).Select(match => match.Value).ToList();
        var isCashForecastCalculation = IsCashForecastCalculationQuestion(input);
        var aliases = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["facture"] = ["facture", "facturation", "document", "fac"],
            ["factures"] = ["facture", "facturation", "document", "fac"],
            ["creer"] = ["creer", "creation", "nouveau", "ajouter", "saisie"],
            ["cree"] = ["creer", "creation", "nouveau", "ajouter", "saisie"],
            ["comment"] = ["comment", "etape", "procedure", "creer", "saisie"],
            ["commen"] = ["comment", "etape", "procedure", "creer", "saisie"],
            ["fac"] = ["facture", "facturation", "document", "fac"],
            ["factur"] = ["facture", "facturation", "document", "fac"],
            ["prediction"] = ["prediction", "predire", "retard", "risque", "modele"],
            ["predition"] = ["prediction", "predire", "retard", "risque", "modele"],
            ["retard"] = ["retard", "risque", "paiement", "relance"],
            ["tva"] = ["tva", "d15", "declaration", "fiscal"],
            ["paiement"] = ["paiement", "encaissement", "reglement", "recu"],
            ["comptabilite"] = ["comptabilite", "ecriture", "journal", "balance"],
            ["prevision"] = ["prevision", "prevoir", "projection", "scenario", "encaissement", "decaissement"],
            ["tresorerie"] = ["tresorerie", "solde", "encaissement", "decaissement", "cashflow", "flux"],
            ["calcul"] = ["calcul", "calcule", "formule", "solde", "scenario"],
            ["calcule"] = ["calcul", "calcule", "formule", "solde", "scenario"],
            ["calculee"] = ["calcul", "calcule", "formule", "solde", "scenario"]
        };

        foreach (var term in terms.ToArray())
        {
            // « Comment est calculée la prévision » est une demande d'explication,
            // pas une demande de procédure de saisie.
            if (isCashForecastCalculation && term is "comment" or "commen") continue;
            if (aliases.TryGetValue(term, out var variants)) terms.AddRange(variants);
            if (term.Length >= 5) terms.Add(term[..Math.Min(5, term.Length)]);
        }

        return terms.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsUsefulChunk(string content)
    {
        var normalized = Normalize(content);
        return !NoiseMarkers.Any(marker => normalized.Contains(Normalize(marker), StringComparison.Ordinal))
            && content.Count(character => character == '|') < 8;
    }

    private static int Score(KnowledgeChunk chunk, string[] terms, string normalizedQuery)
    {
        var content = Normalize(chunk.Content);
        var heading = Normalize(chunk.Heading);
        var exactMatches = terms.Sum(term => Count(content, term) + Count(heading, term) * 4);
        var coverage = terms.Count(term => content.Contains(term, StringComparison.Ordinal) || heading.Contains(term, StringComparison.Ordinal));
        var score = exactMatches + coverage * 3;

        // Une procédure dont le titre reprend les mots métier de la question est
        // beaucoup plus fiable qu'une FAQ où seul le mot « facture » apparaît.
        var questionConcepts = Word.Matches(normalizedQuery)
            .Select(match => match.Value)
            .Where(term => !SearchStopWords.Contains(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var headingCoverage = questionConcepts.Count(term => heading.Contains(term, StringComparison.Ordinal));
        if (headingCoverage >= 2) score += headingCoverage * 25;

        var isCashForecast = normalizedQuery.Contains("prevision", StringComparison.Ordinal)
            && normalizedQuery.Contains("tresorerie", StringComparison.Ordinal);
        if (!isCashForecast) return score;

        // Une correspondance dans le titre d'une section est plus fiable que des
        // mots génériques trouvés dans une procédure voisine.
        if (heading.Contains("prevision", StringComparison.Ordinal)
            && heading.Contains("tresorerie", StringComparison.Ordinal)) score += 60;
        else if (content.Contains("prevision", StringComparison.Ordinal)
            && content.Contains("tresorerie", StringComparison.Ordinal)) score += 15;

        if (IsCashForecastCalculationQuestion(normalizedQuery)
            && heading.Contains("alimenter", StringComparison.Ordinal)) score -= 25;

        return score;
    }

    private static bool HasRelevantEvidence(KnowledgeChunk chunk, string[] evidenceTerms)
    {
        if (evidenceTerms.Length == 0) return false;
        var searchable = $"{Normalize(chunk.Heading)} {Normalize(chunk.Content)}";
        return evidenceTerms.Any(term => searchable.Contains(term, StringComparison.Ordinal));
    }

    private static bool HasSufficientEvidence(string question, IReadOnlyList<KnowledgeSearchResultDto> results)
    {
        if (results.Count == 0) return false;

        var concepts = Word.Matches(Normalize(question))
            .Select(match => match.Value)
            .Where(term => !SearchStopWords.Contains(term))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (concepts.Length == 0) return false;

        var searchable = $"{Normalize(results[0].Heading)} {Normalize(results[0].Content)}";
        var matchedConcepts = concepts.Count(concept => ExpandTerms(concept)
            .Any(term => !SearchStopWords.Contains(term)
                && searchable.Contains(term, StringComparison.Ordinal)));
        var requiredMatches = concepts.Length == 1 ? 1 : 2;

        return results[0].Score >= 8 && matchedConcepts >= requiredMatches;
    }
    private static int Count(string text, string term) => Regex.Matches(text, Regex.Escape(term)).Count;

    private static string BuildReadableFallback(string question, IReadOnlyList<KnowledgeSearchResultDto> results)
    {
        if (IsCashForecastCalculationQuestion(question))
            return "La prévision de trésorerie part du solde bancaire initial. Elle ajoute les encaissements attendus des factures clients et déduit les sorties planifiées, comme les fournisseurs, la paie, les taxes et les autres charges. Le scénario réaliste pondère chaque encaissement selon son risque de retard ; la part à risque est décalée vers une période ultérieure. Les scénarios optimiste et prudent permettent ensuite de comparer les résultats.";

        var isHowTo = IsProcedureQuestion(question);
        if (isHowTo)
        {
            var steps = Sentence.Split(CleanForUser(results[0].Content))
                .Select(sentence => sentence.Trim())
                .Where(sentence => sentence.Length > 18)
                .Take(4)
                .ToArray();
            if (steps.Length == 0)
                return SupportAnswer;
            return string.Join("\n", steps.Select((step, index) => $"{index + 1}. {step}"));
        }

        const int maxSentences = 2;
        var summaries = results
            .Select(result => CleanForUser(result.Content))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => Sentence.Split(text).Take(maxSentences).ToArray())
            .Select(sentences => string.Join(" ", sentences))
            .Where(text => text.Length > 30)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        if (summaries.Length == 0)
            return SupportAnswer;

        return string.Join("\n\n", summaries);
    }

    private static bool IsProcedureQuestion(string question)
    {
        var normalized = Normalize(question);
        if (IsCashForecastCalculationQuestion(normalized)) return false;
        if (!normalized.Contains("comment", StringComparison.Ordinal)
            && !normalized.Contains("commen", StringComparison.Ordinal)) return false;

        return ProcedureVerbs.Any(verb => normalized.Contains(verb, StringComparison.Ordinal));
    }

    private static bool IsCashForecastCalculationQuestion(string question)
    {
        var normalized = Normalize(question);
        var isCashForecast = normalized.Contains("prevision", StringComparison.Ordinal)
            && normalized.Contains("tresorerie", StringComparison.Ordinal);
        if (!isCashForecast) return false;

        return normalized.Contains("calcul", StringComparison.Ordinal)
            || normalized.Contains("formule", StringComparison.Ordinal)
            || normalized.Contains("fonctionne", StringComparison.Ordinal)
            || normalized.Contains("methode", StringComparison.Ordinal)
            || normalized.Contains("mecanisme", StringComparison.Ordinal)
            || normalized.Contains("comment", StringComparison.Ordinal)
            || normalized.Contains("commen", StringComparison.Ordinal);
    }

    private static bool IsSalesVatQuestion(string question)
    {
        var normalized = Normalize(question);
        return normalized.Contains("tva", StringComparison.Ordinal)
            && (normalized.Contains("vente", StringComparison.Ordinal)
                || normalized.Contains("ventes", StringComparison.Ordinal));
    }

    private static string CleanForUser(string text)
    {
        var cleaned = Regex.Replace(text, @"\x60{1,3}.*?\x60{1,3}", string.Empty, RegexOptions.Singleline);
        cleaned = Regex.Replace(cleaned, @"[#*_>|]", " ");
        cleaned = Space.Replace(cleaned, " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+([,.;:!?])", "$1");
        return cleaned.Length > 460 ? cleaned[..460].TrimEnd() + "…" : cleaned;
    }

    private async Task<string?> GenerateWithOllamaAsync(string question, IReadOnlyList<string> extracts, CancellationToken ct)
    {
        if (!bool.TryParse(configuration["Ollama:Enabled"], out var enabled) || !enabled) return null;

        var prompt = $"""
            Tu es l'expert fonctionnel et comptable de TuniFlow. Réponds directement à la question, en français professionnel et clair, uniquement avec les faits présents dans le contexte fourni.
            Ne fais jamais d'hypothèse, n'invente aucune règle fiscale, aucun écran, aucune étape ni donnée. Ne donne jamais une réponse vague ou passe-partout.
            Pour une demande de procédure explicite (créer, ajouter, valider, envoyer…), donne 2 à 4 étapes concrètes. Pour une demande d'explication, donne une réponse précise et concise, sans la transformer en procédure.
            Ne recopie jamais le contexte, les titres de chapitres, les Sprints, les sommaires, les tableaux Markdown ou les noms des fichiers. Ne parle jamais de « contexte », « extrait » ou « documentation validée » dans une réponse réussie.
            Si le contexte ne permet pas de répondre avec certitude, dis exactement : "Je ne peux pas répondre de façon fiable avec la documentation disponible. Contactez l’assistance TuniFlow."

            Question: {question}

            Contexte validé:
            {string.Join("\n\n", extracts)}
            """;
        try
        {
            var client = httpClientFactory.CreateClient("Ollama");
            using var response = await client.PostAsJsonAsync("api/generate", new
            {
                model = configuration["Ollama:Model"] ?? "llama3.2:3b",
                prompt,
                stream = false,
                options = new { temperature = 0.15, num_predict = 220 }
            }, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var payload = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!payload.RootElement.TryGetProperty("response", out var answer)) return null;
            return CleanGeneratedAnswer(answer.GetString());
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    private static string? CleanGeneratedAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;

        var cleaned = answer.Trim();
        var normalized = Normalize(cleaned);
        if (NoiseMarkers.Any(marker => normalized.Contains(Normalize(marker), StringComparison.Ordinal)))
            return null;

        cleaned = Regex.Replace(cleaned, @"(?im)^\s*(chapitre|sprint|backlog|table des matieres|table des matières).*$(\r?\n)?", string.Empty);
        cleaned = Space.Replace(cleaned, " ").Trim();
        return cleaned.Length > 900 ? cleaned[..900].TrimEnd() + "…" : cleaned;
    }
}
