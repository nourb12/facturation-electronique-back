using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Einvoicing.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Einvoicing.Infrastructure.Services;

// Small-corpus exact cosine search. PostgreSQL remains the source of truth for visibility.
// The private cache stores vectors and hashes only, never document text or conversations.
public sealed class KnowledgeVectorIndex(IConfiguration configuration, IWebHostEnvironment environment, IHttpClientFactory clients)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Model => configuration["Rag:EmbeddingModel"] ?? "bge-m3";
    private int Dimension => configuration.GetValue("Rag:EmbeddingDimension", 1024);
    private string Root => Path.GetFullPath(configuration["Rag:IndexDirectory"] ?? Path.Combine(environment.ContentRootPath, ".rag-index"));
    private string IndexFile => Path.Combine(Root, Hash(Model) + ".json");
    public sealed record Ranked(KnowledgeChunk Chunk, double Similarity);
    public sealed record Manifest(string Model, string Digest, int Dimension, string ChunkVersion, Dictionary<string,float[]> Vectors);

    public async Task<IReadOnlyList<Ranked>> SearchAsync(string question, IReadOnlyList<KnowledgeChunk> chunks, int take, CancellationToken ct)
    {
        if (chunks.Count == 0) return [];
        await Gate.WaitAsync(ct);
        try
        {
            var digest = await ModelDigest(ct);
            var state = await Load(digest, ct);
            var unique = chunks.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
            var missing = unique.Where(p => !state.Vectors.ContainsKey(p.Key)).ToArray();
            var batchSize = Math.Clamp(configuration.GetValue("Rag:EmbeddingBatchSize", 8), 1, 50);
            foreach (var batch in missing.Chunk(batchSize))
            {
                var vectors = await Embed(batch.Select(pair => Input(pair.Value)).ToArray(), ct);
                for (var i = 0; i < batch.Length; i++) state.Vectors[batch[i].Key] = vectors[i];
                await Save(state, ct);
            }
            var query = (await Embed([question], ct))[0];
            if (await ModelDigest(ct) != digest) throw new InvalidOperationException("Le modèle d'embedding a changé pendant la recherche. Relancez une réindexation explicite.");
            return chunks.Select(chunk => new Ranked(chunk, Cosine(query, state.Vectors[Key(chunk)])))
                .OrderByDescending(x => x.Similarity).Take(take).ToArray();
        }
        catch (HttpRequestException e) { throw new InvalidOperationException("Recherche sémantique indisponible : vérifiez Ollama et le modèle bge-m3.", e); }
        catch (TaskCanceledException e) when(!ct.IsCancellationRequested) {throw new InvalidOperationException("Le service d'embedding n'a pas répondu dans le délai prévu.",e);}
        catch (JsonException e) {throw new InvalidOperationException("Réponse Ollama ou index vectoriel illisible.",e);}
        finally { Gate.Release(); }
    }

    public async Task ResetAsync(bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw new ArgumentException("La réinitialisation de l'index doit être confirmée explicitement.");
        await Gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Root);
            if (File.Exists(IndexFile)) File.Move(IndexFile, IndexFile + "." + Guid.NewGuid().ToString("N") + ".backup");
        }
        finally { Gate.Release(); }
    }
    private async Task<string> ModelDigest(CancellationToken ct)
    {
        var client = clients.CreateClient("OllamaEmbeddings");
        using var response = await client.GetAsync("api/tags", ct); response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var expected = Model.Contains(':') ? Model : Model + ":latest";
        foreach (var entry in payload.RootElement.GetProperty("models").EnumerateArray())
            if (entry.GetProperty("name").GetString() == expected)
                return entry.GetProperty("digest").GetString() ?? throw new InvalidOperationException("Empreinte du modèle absente.");
        throw new InvalidOperationException($"Le modèle d'embedding {Model} n'est pas installé dans Ollama. Installez-le avant la réindexation.");
    }
    private async Task<Manifest> Load(string digest, CancellationToken ct)
    {
        if (!File.Exists(IndexFile)) return new(Model,digest,Dimension,KnowledgeText.Version,[]);
        await using var input = File.OpenRead(IndexFile);
        var state = await JsonSerializer.DeserializeAsync<Manifest>(input, Json, ct) ?? throw new InvalidOperationException("Index vectoriel illisible.");
        if (state.Model != Model || state.Digest != digest || state.Dimension != Dimension || state.ChunkVersion != KnowledgeText.Version)
            throw new InvalidOperationException("Index incompatible avec le modèle ou le découpage actuel. Une réindexation explicite est nécessaire; l'ancien index est conservé.");
        if (state.Vectors.Values.Any(v => v.Length != Dimension || v.Any(x => !float.IsFinite(x)))) throw new InvalidOperationException("Vecteurs stockés incompatibles ou corrompus.");
        return state;
    }
    private async Task Save(Manifest state, CancellationToken ct)
    {
        Directory.CreateDirectory(Root); var temporary = IndexFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, Json), ct); File.Move(temporary, IndexFile, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private async Task<float[][]> Embed(string[] inputs, CancellationToken ct)
    {
        for (var attempt=0; ; attempt++)
        {
            try
            {
                using var response = await clients.CreateClient("OllamaEmbeddings").PostAsJsonAsync("api/embed", new { model=Model, input=inputs, truncate=false, keep_alive="30m" },ct);
                response.EnsureSuccessStatusCode();
                using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken:ct);
                var vectors = payload.RootElement.GetProperty("embeddings").EnumerateArray().Select(v => v.EnumerateArray().Select(x => x.GetSingle()).ToArray()).ToArray();
                if (vectors.Length != inputs.Length || vectors.Any(v => v.Length != Dimension || v.Any(x => !float.IsFinite(x))))
                    throw new InvalidOperationException("Dimension ou contenu inattendu des embeddings Ollama.");
                return vectors;
            }
            catch (HttpRequestException) when(attempt<2) { await Task.Delay(TimeSpan.FromMilliseconds(300*Math.Pow(2,attempt)),ct); }
        }
    }
    private static string Input(KnowledgeChunk c) => $"{c.Document?.Title} > {c.Heading}\n{c.Content}";
    private static string Key(KnowledgeChunk c) => Hash(Input(c));
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static double Cosine(float[] a,float[] b)
    {
        if(a.Length!=b.Length)throw new InvalidOperationException("Dimensions vectorielles incompatibles.");
        double dot=0,aa=0,bb=0;for(var i=0;i<a.Length;i++){dot+=(double)a[i]*b[i];aa+=(double)a[i]*a[i];bb+=(double)b[i]*b[i];}
        return aa==0||bb==0?0:Math.Clamp(dot/Math.Sqrt(aa*bb),-1,1);
    }
}
