using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

var phase = args.FirstOrDefault() ?? "before";
var root = FindRepository();
var corpus = args.Skip(1).FirstOrDefault() ?? "C:/frontendpfe/ey-invoice-portal/docs/rag-knowledge-base";
var output = Path.Combine(root, ".rag-evaluation"); Directory.CreateDirectory(output);
if(phase=="before"&&File.Exists(Path.Combine(output,"before.json")))throw new InvalidOperationException("La mesure avant modification est conservée. Utilisez un autre nom pour une nouvelle exécution.");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var questions = JsonSerializer.Deserialize<List<Question>>(await File.ReadAllTextAsync(Path.Combine(root,"tools/RagEvaluation/questions.json")),json)!;
var threshold="0.462";
if(File.Exists(Path.Combine(output,"calibration.json"))) {
 using var calibration=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output,"calibration.json")));
 threshold=calibration.RootElement.GetProperty("selectedThreshold").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
}
if(phase=="generation-smoke")questions=questions.Where(q=>q.Id is "q01" or "q05" or "q31" or "q33").ToList();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
 ["Ollama:Enabled"]=phase=="generation-smoke"?"true":"false",["Rag:SemanticEnabled"]= phase == "before" ? "false" : "true",
 ["Rag:IndexDirectory"]=Path.Combine(output,"index"),["Rag:SimilarityThreshold"] = Environment.GetEnvironmentVariable("RAG_THRESHOLD") ?? threshold
}).Build();
await using var db = new ContextBaseDeDonnees(new DbContextOptionsBuilder<ContextBaseDeDonnees>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
var service = new KnowledgeBaseService(db,new EvaluationEnvironment(output),config,new EvaluationHttpFactory());
var names = new Dictionary<Guid,string>(); var hashes = new Dictionary<string,string>();
foreach (var file in new[]{"01-guide-fonctionnel-tuniflow.md","02-comptabilite-et-tresorerie.md","03-analyse-et-ml.md","05-faq-assistant.md","08-securite-et-roles.md","11-procedures-utilisateur.md"}) {
 var bytes=await File.ReadAllBytesAsync(Path.Combine(corpus,file));hashes[file]=Convert.ToHexString(SHA256.HashData(bytes));
 await using var stream=new MemoryStream(bytes);
 var doc=await service.ImportAsync(Guid.Empty,Path.GetFileNameWithoutExtension(file),"Corpus de test","Dépôt local - validation en mémoire seulement",null,stream,file,"text/markdown");
 await service.SetStatusAsync(doc.Id,"Valide",Guid.Empty);names[doc.Id]=file;
}
Console.WriteLine($"Corpus: {names.Count} documents; {await db.KnowledgeChunks.CountAsync()} passages. Aucun changement dans PostgreSQL.");
var rows=new List<object>(); int hit=0,sectionHit=0,refused=0,positives=0,negatives=0;
var timings=new List<double>();
foreach (var q in questions) {
 var sw=Stopwatch.StartNew();var found=await service.SearchAsync(q.Q,5);sw.Stop();
 // Generation disabled: Found measures acceptance by the retrieval gate, not LLM answer correctness.
 var answerTimer=Stopwatch.StartNew();var answer=await service.AskAsync(q.Q);answerTimer.Stop();timings.Add(sw.Elapsed.TotalMilliseconds);
 var correct=found.Any(r=>names.GetValueOrDefault(r.DocumentId)==q.Doc);
 var section=found.Any(r=>names.GetValueOrDefault(r.DocumentId)==q.Doc && Fold(r.Heading).Contains(Fold(q.Section??"")));
 if(q.Doc is null){negatives++;if(!answer.Found)refused++;}else{positives++;if(correct)hit++;if(section)sectionHit++;}
 rows.Add(new{id=q.Id,split=q.Split,question=q.Q,expectedDocument=q.Doc,expectedSection=q.Section,correctDocumentAt5=correct,correctSectionAt5=section,accepted=answer.Found,mode=answer.Mode,answer=answer.Answer,warning=answer.Warning,elapsedMs=Math.Round(sw.Elapsed.TotalMilliseconds,2),answerMs=Math.Round(answerTimer.Elapsed.TotalMilliseconds,2),results=found.Select(r=>new{document=names.GetValueOrDefault(r.DocumentId),r.Heading,r.Score,r.Similarity})});
 Console.WriteLine($"{q.Id} {q.Split}: doc@5={correct} section@5={section} accepted={answer.Found} {sw.Elapsed.TotalMilliseconds:F0}ms");
}
var questionBytes=await File.ReadAllBytesAsync(Path.Combine(root,"tools/RagEvaluation/questions.json"));
var report=new{phase,createdUtc=DateTime.UtcNow,threshold=config["Rag:SimilarityThreshold"],embeddingModel="bge-m3",embeddingDimension=1024,generationEnabled=phase=="generation-smoke",scope="Corpus documentaire du dépôt, importé et validé uniquement dans EF InMemory. Recherche réelle. Questions rédigées pour le test, non issues de retours utilisateurs. La qualité des réponses générées nécessite une lecture humaine.",documents=hashes,questionsSha256=Convert.ToHexString(SHA256.HashData(questionBytes)),positiveQuestions=positives,negativeQuestions=negatives,documentRecallAt5=(double)hit/positives,sectionRecallAt5=(double)sectionHit/positives,outOfScopeRefusalRate=(double)refused/negatives,medianSearchMs=timings.Order().ElementAt(timings.Count/2),results=rows};
var dest=Path.Combine(output,$"{phase}.json");await File.WriteAllTextAsync(dest,JsonSerializer.Serialize(report,json));Console.WriteLine(dest);
static string Fold(string s)=>string.Concat(s.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark));
static string FindRepository() {
 foreach(var start in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory})
  for(var dir=new DirectoryInfo(start);dir is not null;dir=dir.Parent)
   if(File.Exists(Path.Combine(dir.FullName,"tools","RagEvaluation","questions.json")))return dir.FullName;
 throw new DirectoryNotFoundException("Exécutez l'évaluation depuis le dépôt backend.");
}
record Question(string Id,string Split,string Q,string? Doc,string? Section);
sealed class EvaluationEnvironment(string root):IWebHostEnvironment {
 public string ApplicationName {get;set;}="RagEvaluation";public string EnvironmentName{get;set;}="Test";public string ContentRootPath{get;set;}=root;
 public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}="";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
}
sealed class EvaluationHttpFactory:IHttpClientFactory {public HttpClient CreateClient(string name)=>new(){BaseAddress=new Uri("http://127.0.0.1:11434/"),Timeout=TimeSpan.FromSeconds(name=="Ollama"?10:120)};}
