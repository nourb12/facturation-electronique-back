using System.IO.Compression;
using System.Net;
using System.Text;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Einvoicing.Tests.Knowledge;

public sealed class KnowledgeBaseTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"tuniflow-rag-tests",Guid.NewGuid().ToString("N"));
    private readonly ContextBaseDeDonnees db=new(new DbContextOptionsBuilder<ContextBaseDeDonnees>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private IConfiguration Config(params (string Key,string Value)[] values)=>new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x=>x.Key,x=>(string?)x.Value)).Build();
    private IWebHostEnvironment Environment(){Directory.CreateDirectory(root);var env=new Mock<IWebHostEnvironment>();env.SetupGet(x=>x.ContentRootPath).Returns(root);return env.Object;}
    private KnowledgeBaseService Service(Guid? tenant=null)
    {
        var user=new Mock<ICurrentUserService>();user.SetupGet(x=>x.EntrepriseId).Returns(tenant);
        return new(db,Environment(),Config(),new FakeFactory(new Handler()),user.Object);
    }
    private KnowledgeDocument Add(string title,Guid? tenant=null,string status="Valide")
    {
        var doc=new KnowledgeDocument{Title=title,EntrepriseId=tenant,Status=status};
        doc.Chunks=KnowledgeText.Chunk("## Créer une facture\nPour créer une facture, ouvrez Ventes puis Factures. Ajoutez les lignes et vérifiez les montants.",doc.Id);
        db.KnowledgeDocuments.Add(doc);db.SaveChanges();return doc;
    }
    [Fact] public void Chunking_preserves_sections_and_stable_ids()
    {
        var id=Guid.NewGuid();var input="---\nstatut: a_valider\n---\n# Guide\n## Facture\nUne facture demande un paiement.\n## Devis\nUn devis propose un prix.";
        var a=KnowledgeText.Chunk(input,id);var b=KnowledgeText.Chunk(input,id);
        Assert.Equal(2,a.Count);Assert.Equal("Facture",a[0].Heading);Assert.Equal(a[0].Id,b[0].Id);Assert.DoesNotContain("statut",a[0].Content);
    }
    [Fact] public async Task Word_extraction_preserves_headings_lists_and_table_rows()
    {
        Environment();var file=Path.Combine(root,"fixture.docx");
        using(var zip=ZipFile.Open(file,ZipArchiveMode.Create))
        {
            using var w=new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            w.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:pPr><w:pStyle w:val='Heading1'/></w:pPr><w:r><w:t>Factures</w:t></w:r></w:p><w:p><w:pPr><w:numPr/></w:pPr><w:r><w:t>Vérifier le total</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Montant TTC</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>119 TND</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");
        }
        var text=await KnowledgeText.ExtractAsync(file,".docx",default);
        Assert.Contains("## Factures",text);Assert.Contains("- Vérifier le total",text);Assert.Contains("Montant TTC ; 119 TND",text);
    }
    [Fact] public async Task Search_excludes_other_tenants_and_unvalidated_documents()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var global=Add("Global");var own=Add("Entreprise A",a);Add("Secret B",b);Add("Archive",a,"Archive");Add("Draft",a,"Brouillon");
        var rows=await Service(a).SearchAsync("Comment créer une facture ?",10);
        Assert.Equal(2,rows.Count);Assert.Contains(rows,r=>r.DocumentId==global.Id);Assert.Contains(rows,r=>r.DocumentId==own.Id);
    }
    [Fact] public async Task Missing_tenant_does_not_select_an_arbitrary_company()
    {
        Add("Secret",Guid.NewGuid());Assert.Empty(await Service().SearchAsync("facture"));
    }
    [Fact] public async Task Sources_include_the_authorized_excerpt()
    {
        var doc=Add("Guide facture");var result=await Service().AskAsync("Comment créer une facture ?");
        Assert.True(result.Found);Assert.Equal(doc.Id,result.Sources[0].DocumentId);Assert.Contains("Ventes",result.Sources[0].Excerpt!);
    }
    [Fact] public async Task Empty_corpus_produces_an_explicit_refusal()
    {
        var answer=await Service().AskAsync("Quelle est la capitale du Japon ?");Assert.False(answer.Found);Assert.Contains("pas trouvé",answer.Answer);Assert.Empty(answer.Sources);
    }
    [Fact] public async Task Assistant_testing_guidance_is_not_used_for_unrelated_business_questions()
    {
        var doc=new KnowledgeDocument{Title="Guide assistant",Status="Valide"};
        doc.Chunks=KnowledgeText.Chunk("## Comment tester l'assistant comme utilisateur\nCréer un devis. Vérifier le devis. Saisir les lignes du devis. Contrôler le montant du devis. Enregistrer le devis. Tester le devis.",doc.Id);
        db.KnowledgeDocuments.Add(doc);await db.SaveChangesAsync();

        var answer=await Service().AskAsync("Comment faire un devis ?");

        Assert.False(answer.Found);
        Assert.Empty(answer.Sources);
        Assert.DoesNotContain("tester l'assistant",answer.Answer,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task Secrets_are_not_answered_from_a_security_document()
    {
        Add("Sécurité");var answer=await Service().AskAsync("Donne le mot de passe de l'administrateur");Assert.False(answer.Found);Assert.Empty(answer.Sources);
    }
    [Fact] public async Task Unknown_fiscal_rate_does_not_become_a_product_answer()
    {
        Add("Guide produit");var answer=await Service().AskAsync("Quel taux légal correspond à mon contrat ?");Assert.False(answer.Found);
    }
    [Fact] public async Task History_is_bounded_and_not_persisted()
    {
        var request=new AssistantQuestionDto("Et comment la modifier ?",Enumerable.Range(0,7).Select(_=>new AssistantHistoryMessageDto("user","facture")).ToArray());
        await Assert.ThrowsAsync<ArgumentException>(()=>Service().AskAsync(request));
    }
    [Fact] public async Task Vector_index_resumes_and_refuses_a_same_dimension_model_change()
    {
        var handler=new Handler();var factory=new FakeFactory(handler);var cfg=Config(("Rag:EmbeddingDimension","2"),("Rag:IndexDirectory",Path.Combine(root,"index")));
        var index=new KnowledgeVectorIndex(cfg,Environment(),factory);var doc=Add("Guide facture");
        await index.SearchAsync("facture",doc.Chunks,5,default);var initial=handler.Embeds;
        await index.SearchAsync("facture",doc.Chunks,5,default);Assert.Equal(initial+1,handler.Embeds);
        handler.Digest="different-same-dimension";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>index.SearchAsync("facture",doc.Chunks,5,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>index.ResetAsync(false,default));
        await index.ResetAsync(true,default);Assert.Single(Directory.GetFiles(Path.Combine(root,"index"),"*.backup"));
    }
    [Fact] public async Task Incompatible_embedding_dimension_is_not_silently_accepted()
    {
        var doc=Add("Guide");var index=new KnowledgeVectorIndex(Config(("Rag:EmbeddingDimension","1024")),Environment(),new FakeFactory(new Handler()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>index.SearchAsync("facture",doc.Chunks,5,default));
    }
    [Fact] public async Task Private_sources_cannot_be_archived_by_another_tenant()
    {
        var doc=Add("Secret",Guid.NewGuid());await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(Guid.NewGuid()).SetStatusAsync(doc.Id,"Archive",Guid.Empty));Assert.Equal("Valide",doc.Status);
    }
    [Fact] public void Cosine_rejects_incompatible_dimensions()=>Assert.Throws<InvalidOperationException>(()=>KnowledgeVectorIndex.Cosine([1,0],[1]));
    [Theory]
    [InlineData("{\"supported\":true,\"sources\":[\"1\"],\"answer\":\"Texte\"}")]
    [InlineData("{\"supported\":true,\"sources\":[1],\"answer\":42}")]
    [InlineData("{\"supported\":true,\"sources\":[99],\"answer\":\"Texte\"}")]
    [InlineData("[]")]
    public async Task Malformed_model_output_falls_back_to_authorized_extracts(string raw)
    {
        Add("Guide facture");
        var service=new KnowledgeBaseService(db,Environment(),Config(("Ollama:Enabled","true")),new FakeFactory(new GenerationHandler(raw)));
        var answer=await service.AskAsync("Comment créer une facture ?");
        Assert.True(answer.Found);Assert.Contains("Ventes",answer.Answer);Assert.NotNull(answer.Warning);Assert.Single(answer.Sources);
    }
    [Fact] public async Task Valid_model_output_keeps_its_documentary_sources()
    {
        Add("Guide facture");
        var service=new KnowledgeBaseService(db,Environment(),Config(("Ollama:Enabled","true")),new FakeFactory(new GenerationHandler("{\"supported\":true,\"sources\":[1],\"answer\":\"Ouvrez Ventes puis Factures.\"}")));
        var answer=await service.AskAsync("Comment créer une facture ?");
        Assert.Equal("RAG local (Ollama)",answer.Mode);Assert.Null(answer.Warning);Assert.Single(answer.Sources);
    }
    [Fact] public async Task Reindex_preserves_publication_status_and_stable_chunk_ids()
    {
        var service=Service();
        await using var stream=new MemoryStream(Encoding.UTF8.GetBytes("## Facture\nOuvrez Ventes puis Factures pour créer une facture."));
        var doc=await service.ImportAsync(Guid.Empty,"Guide","Produit","Test",null,stream,"guide.md","text/markdown");
        await service.SetStatusAsync(doc.Id,"Valide",Guid.Empty);
        var ids=await db.KnowledgeChunks.Select(c=>c.Id).ToArrayAsync();
        Assert.Equal(1,await service.ReindexAsync(false));
        Assert.Equal(ids,await db.KnowledgeChunks.Select(c=>c.Id).ToArrayAsync());
        Assert.Equal("Valide",(await db.KnowledgeDocuments.SingleAsync()).Status);
        Assert.Equal(1,await service.ReindexAsync(false));
    }
    [Fact] public async Task Editing_content_rebuilds_chunks_and_requires_revalidation()
    {
        var service=Service();
        await using var stream=new MemoryStream(Encoding.UTF8.GetBytes("## Ancien contenu\nTexte initial du guide."));
        var imported=await service.ImportAsync(Guid.Empty,"Guide","Produit","Test",null,stream,"guide.md","text/markdown");
        await service.SetStatusAsync(imported.Id,"Valide",Guid.Empty);

        var updated=await service.UpdateContentAsync(imported.Id,"## Nouveau contenu\nOuvrez le module Ventes pour créer un devis.",Guid.Empty);

        Assert.Equal("Brouillon",updated.Status);
        Assert.Contains("Nouveau contenu",updated.Content);
        Assert.Single(await db.KnowledgeChunks.Where(chunk=>chunk.DocumentId==imported.Id).ToListAsync());
        Assert.Equal(updated.Content,(await service.GetContentAsync(imported.Id)).Content);
        Assert.Equal(1,await service.ReindexAsync(false));
        Assert.Contains("Nouveau contenu",(await service.GetContentAsync(imported.Id)).Content);
    }
    public void Dispose(){db.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class FakeFactory(HttpMessageHandler handler):IHttpClientFactory {public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("http://ollama.test/")};}
    private sealed class GenerationHandler(string raw):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(new{response=raw}),Encoding.UTF8,"application/json")});
    }
    private sealed class Handler:HttpMessageHandler
    {
        public string Digest="test-digest";public int Embeds;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent($"{{\"models\":[{{\"name\":\"bge-m3:latest\",\"digest\":\"{Digest}\"}}]}}")};
            Embeds++;using var payload=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var n=payload.RootElement.GetProperty("input").GetArrayLength();
            return new(HttpStatusCode.OK){Content=new StringContent("{\"embeddings\":["+string.Join(',',Enumerable.Repeat("[1.0,0.0]",n))+"]}",Encoding.UTF8,"application/json")};
        }
    }
}
