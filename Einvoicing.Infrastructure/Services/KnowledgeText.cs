using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Einvoicing.Domain.Entities;

using Einvoicing.Domain;

namespace Einvoicing.Infrastructure.Services;

public static class KnowledgeText
{
    public const string Version = "sections-v2";
    public static async Task<string> ExtractAsync(string path, string extension, CancellationToken ct)
    {
        if (extension is ".txt" or ".md") return await File.ReadAllTextAsync(path, ct);
        using var zip = ZipFile.OpenRead(path);
        var part = zip.GetEntry("word/document.xml") ?? throw new ArgumentException("Document Word invalide.");
        if (part.Length > 20 * 1024 * 1024) throw new ArgumentException("Le contenu Word décompressé dépasse 20 Mo.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var headingStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (zip.GetEntry("word/styles.xml") is { } styles)
        {
            if (styles.Length > 5 * 1024 * 1024) throw new ArgumentException("Les styles Word décompressés dépassent 5 Mo.");
            await using var styleStream = styles.Open();
            var styleXml = await XDocument.LoadAsync(styleStream, LoadOptions.None, ct);
            foreach (var style in styleXml.Descendants(w + "style"))
            {
                var name = (string?)style.Element(w + "name")?.Attribute(w + "val") ?? "";
                if (Regex.IsMatch(name, @"^(heading|titre)\s*\d", RegexOptions.IgnoreCase, RegexMatchPolicy.Timeout) || style.Descendants(w + "outlineLvl").Any())
                    headingStyles.Add((string?)style.Attribute(w + "styleId") ?? "");
            }
        }
        await using var stream = part.Open();
        var xml = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        var body = xml.Root?.Element(w + "body") ?? throw new ArgumentException("Document Word sans contenu.");
        string Text(XElement p) => string.Concat(p.Descendants().Where(e => e.Name == w + "t" || e.Name == w + "tab" || e.Name == w + "br").Select(e => e.Name == w + "t" ? e.Value : " ")).Trim();
        var lines = new List<string>();
        foreach (var block in body.Elements())
        {
            if (block.Name == w + "p")
            {
                var text = Text(block); if (text.Length == 0) continue;
                var style = (string?)block.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val") ?? "";
                var heading = headingStyles.Contains(style) || Regex.IsMatch(style, @"^(heading|titre)\d", RegexOptions.IgnoreCase, RegexMatchPolicy.Timeout);
                lines.Add((heading ? "## " : block.Descendants(w + "numPr").Any() ? "- " : "") + text);
            }
            else if (block.Name == w + "tbl")
            {
                foreach (var row in block.Elements(w + "tr"))
                    lines.Add(string.Join(" ; ", row.Elements(w + "tc").Select(cell => string.Join(" ", cell.Descendants(w + "p").Select(Text)))));
            }
        }
        return string.Join("\n", lines);
    }

    public static List<KnowledgeChunk> Chunk(string text, Guid documentId, int maxWords = 300)
    {
        maxWords = Math.Clamp(maxWords, 80, 500);
        text = Regex.Replace(text.Replace("\r", ""), @"\A---\n[\s\S]*?\n---\n", "", RegexOptions.None, RegexMatchPolicy.Timeout);
        var chunks = new List<KnowledgeChunk>(); var section = "Document"; var parent = "";
        var buffer = new List<string>(); var count = 0;
        void Flush()
        {
            if (buffer.Count == 0) return;
            var content = string.Join("\n", buffer).Trim();
            chunks.Add(new KnowledgeChunk { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{documentId}/{section}/{chunks.Count}/{content}"))[..16]), DocumentId = documentId, Position = chunks.Count, Heading = section[..Math.Min(section.Length, 300)], Content = content });
            buffer.Clear(); count = 0;
        }
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim(); if (line.Length == 0) continue;
            if (line.StartsWith('#'))
            {
                Flush(); var level = line.TakeWhile(c => c == '#').Count(); var title = line.TrimStart('#', ' ');
                if (level <= 2) { section = title; parent = title; }
                else section = string.IsNullOrEmpty(parent) ? title : parent + " > " + title;
                continue;
            }
            // Sentences remain intact, even when a single sentence exceeds the target size.
            foreach (var sentence in Regex.Split(line, @"(?<=[.!?])\s+", RegexOptions.None, RegexMatchPolicy.Timeout))
            {
                var words = Regex.Matches(sentence, @"\S+", RegexOptions.None, RegexMatchPolicy.Timeout).Count;
                if (count > 0 && count + words > maxWords) Flush();
                buffer.Add(sentence); count += words;
            }
        }
        Flush(); return chunks;
    }
}
