using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Api.Controllers;
using Einvoicing.Api.Middleware;
using Einvoicing.Api.Services;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Einvoicing.Tests.Integration;

// Actual HTTP routing, JWT validation, policies, controllers and repositories;
// the database is isolated in memory, not the running PostgreSQL instance.
public sealed class CommercialAccessHost : IDisposable
{
    private const string SigningKey = "test-only-tuniflow-signing-key-not-for-production-0123456789";
    private readonly TestServer server;
    private int sequence;
    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();
    public Client ClientA { get; }
    public Client ClientB { get; }
    public Facture QuoteB { get; }
    public Transaction TransactionA { get; }
    public Transaction TransactionB { get; }

    public CommercialAccessHost()
    {
        var database = Guid.NewGuid().ToString();
        server = new TestServer(new WebHostBuilder().ConfigureLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning)).ConfigureServices(services =>
        {
            services.AddDbContext<ContextBaseDeDonnees>(o => o.UseInMemoryDatabase(database));
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFactureRepository, FactureRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IClientService, ClientService>();
            services.AddScoped<IEntrepriseRepository, EntrepriseRepository>();
            services.AddScoped<IPersonnalisationRepository, PersonnalisationRepository>();
            services.AddScoped<IFactureService, FactureService>();
            services.AddScoped<ITeifService, TeifService>();
            services.AddSingleton(Mock.Of<ILatePaymentPredictionService>());
            services.AddSingleton(Mock.Of<IRelanceService>());
            var numbers = new Mock<INumeroFactureService>();
            numbers.Setup(n => n.GenererNumeroAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => $"TEST-{Interlocked.Increment(ref sequence):D5}");
            services.AddSingleton(numbers.Object);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateIssuer = true, ValidIssuer = "integration-tests",
                    ValidateAudience = true, ValidAudience = "integration-tests",
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                };
                o.Events = new JwtBearerEvents { OnTokenValidated = ActiveSessionValidation.Valider };
            });
            services.AddTuniFlowAuthorization();
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<CreerFactureValidator>();
            services.AddControllers().AddApplicationPart(typeof(FacturesController).Assembly)
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }).Configure(app =>
        {
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseRouting();
            app.UseAuthentication();
            app.UseMiddleware<SupportReadOnlyMiddleware>();
            app.UseMiddleware<CollaborateurVentesAccessMiddleware>();
            app.UseAuthorization();
            app.UseEndpoints(e => e.MapControllers());
        }));
        ClientA = Client.Creer(TenantA, "Client entreprise A", "client-a@test.invalid", TypeClient.B2B, "1234567ABM000");
        ClientB = Client.Creer(TenantB, "Client entreprise B", "client-b@test.invalid", TypeClient.B2B, "1234568ABM000");
        QuoteB = Facture.Creer(TenantB, ClientB.Id, Guid.NewGuid(), "DEV-B-SECRET", TypeFacture.Devis,
            ModePaiement.Virement, DateTime.UtcNow.AddDays(30));
        QuoteB.AjouterLigne(LigneFacture.Creer(QuoteB.Id, 1, "Service entreprise B", 1, 100, 19));
        TransactionA = Transaction.Creer(TenantA, Guid.NewGuid(), DateTime.UtcNow, "Transaction A", 119, TypeTransaction.Entree);
        TransactionB = Transaction.Creer(TenantB, Guid.NewGuid(), DateTime.UtcNow, "Transaction B", 119, TypeTransaction.Entree);
        WithDb(db =>
        {
            db.Clients.AddRange(ClientA, ClientB);
            db.Factures.Add(QuoteB);
            db.Transactions.AddRange(TransactionA, TransactionB);
            foreach (var tenant in new[] { TenantA, TenantB })
                db.DocumentsCommerciaux.Add(DocumentCommercial.Creer(tenant,
                    tenant == TenantA ? ClientA.Id : ClientB.Id, Guid.NewGuid(), "bon_commande",
                    tenant == TenantA ? "BC-A" : "BC-B-SECRET", DateTime.UtcNow, null, 100, 19, 119,
                    "[]", null, null));
        });
    }

    public HttpClient Connect(RoleUtilisateur role, Guid? tenant, out Guid userId)
    {
        var user = Utilisateur.Creer("Test", "Acces", $"{Guid.NewGuid():N}@test.invalid", "not-a-real-password", role, tenant);
        var jti = Guid.NewGuid().ToString();
        WithDb(db =>
        {
            db.Utilisateurs.Add(user);
            db.RefreshTokens.Add(RefreshToken.Creer(user.Id, Guid.NewGuid().ToString(), jti, null, null));
        });
        userId = user.Id;
        var token = new JwtSecurityToken("integration-tests", "integration-tests",
            [new Claim("userId", user.Id.ToString()), new Claim("entrepriseId", tenant?.ToString() ?? ""),
             new Claim(ClaimTypes.Role, role.ToString()), new Claim(JwtRegisteredClaimNames.Jti, jti)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    public HttpClient Anonymous() => server.CreateClient();

    public void WithDb(Action<ContextBaseDeDonnees> action)
    {
        using var scope = server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContextBaseDeDonnees>();
        action(db);
        db.SaveChanges();
    }

    public object QuoteRequest(Guid? clientId = null, string type = "Devis") => new
    {
        clientId = clientId ?? ClientA.Id, typeFacture = type, modePaiement = "Virement",
        dateEcheance = DateTime.UtcNow.AddDays(30), devise = "TND",
        lignes = new[] { new { designation = "Conseil", quantite = 2, prixUnitaire = 100, tauxTva = 19, unite = "U", tauxRemise = 0 } }
    };

    public void Dispose() => server.Dispose();
}

public sealed class CommercialAccessTests(CommercialAccessHost host) : IClassFixture<CommercialAccessHost>
{
    [Theory]
    [InlineData("/api/factures")]
    [InlineData("/api/documents-commerciaux")]
    public async Task Anonymous_requests_are_rejected(string url)
    {
        using var client = host.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Sales_user_can_create_filter_edit_and_convert_a_quote_over_http()
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        var response = await client.PostAsJsonAsync("/api/factures", host.QuoteRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<JsonElement>());
        var id = quote.GetProperty("id").GetGuid();
        Assert.Equal("Devis", quote.GetProperty("typeFacture").GetString());
        Assert.Equal(238m, quote.GetProperty("totalTtc").GetDecimal());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/factures?typeFacture=Devis");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.DoesNotContain("DEV-B-SECRET", list.ToString());

        var update = await client.PutAsJsonAsync($"/api/factures/{id}", new
        {
            modePaiement = "Virement", dateEcheance = DateTime.UtcNow.AddDays(35), notes = "Devis corrige",
            lignes = new[] { new { designation = "Conseil modifie", quantite = 3, prixUnitaire = 100, tauxTva = 19, unite = "U", tauxRemise = 0 } }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var edited = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(357m, edited.GetProperty("totalTtc").GetDecimal());
        var conversion = await client.PostAsJsonAsync($"/api/factures/{id}/convertir-facture", new { });
        Assert.Equal(HttpStatusCode.Created, conversion.StatusCode);
        var invoice = await conversion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Facture", invoice.GetProperty("typeFacture").GetString());
        Assert.Equal(id, invoice.GetProperty("factureOrigineId").GetGuid());
        Assert.Equal(357m, invoice.GetProperty("totalTtc").GetDecimal());
    }

    [Fact]
    public async Task Unknown_document_types_are_rejected()
    {
        using var client = host.Connect(RoleUtilisateur.ResponsableEntreprise, host.TenantA, out _);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/factures", host.QuoteRequest(type: "Inconnu"))).StatusCode);
    }

    [Fact]
    public async Task Foreign_client_cannot_be_used_to_create_an_invoice()
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/factures", host.QuoteRequest(host.ClientB.Id))).StatusCode);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/historique")]
    [InlineData("GET", "/pdf")]
    [InlineData("POST", "/valider")]
    [InlineData("POST", "/convertir-facture")]
    public async Task Company_A_cannot_access_or_change_company_B_invoice(string method, string suffix)
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/factures/{host.QuoteB.Id}{suffix}");
        if (method == "POST") request.Content = JsonContent.Create(new { });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("DEV-B-SECRET", await response.Content.ReadAsStringAsync());
        host.WithDb(db => Assert.Equal(StatutFacture.Brouillon, db.Factures.Single(f => f.Id == host.QuoteB.Id).Statut));
    }

    [Fact]
    public async Task Company_A_cannot_edit_company_B_invoice()
    {
        using var client = host.Connect(RoleUtilisateur.ResponsableEntreprise, host.TenantA, out _);
        var response = await client.PutAsJsonAsync($"/api/factures/{host.QuoteB.Id}", host.QuoteRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Commercial_document_list_is_tenant_scoped()
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        var response = await client.GetAsync("/api/documents-commerciaux");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("BC-A", text);
        Assert.DoesNotContain("BC-B-SECRET", text);
    }

    [Fact]
    public async Task Commercial_document_writes_are_allowed_for_sales_but_not_other_tenants()
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        var response = await client.PostAsJsonAsync("/api/documents-commerciaux", new
        {
            type = "bon_commande", clientId = host.ClientA.Id, numero = $"BC-{Guid.NewGuid():N}",
            date = DateTime.UtcNow, statut = "Brouillon",
            lignes = new[] { new { designation = "Service", quantite = 1, prixUnitaire = 100, tauxTva = 19 } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Guid foreignId = Guid.Empty;
        host.WithDb(db => foreignId = db.DocumentsCommerciaux.First(d => d.EntrepriseId == host.TenantB).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/documents-commerciaux/{foreignId}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/paiements")]
    [InlineData("/api/comptabilite/dashboard")]
    [InlineData("/api/admin/utilisateurs")]
    [InlineData("/api/factures-fournisseur")]
    [InlineData("/api/clients-admin")]
    public async Task Sales_role_cannot_reach_finance_administration_or_prefix_lookalikes(string url)
    {
        using var client = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out _);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("/api/documents/dashboard")]
    [InlineData("/api/comptabilite/dashboard")]
    [InlineData("/api/paie/dashboard")]
    [InlineData("/api/documents-commerciaux")]
    public async Task Missing_tenant_never_selects_another_company(string url)
    {
        using var client = host.Connect(RoleUtilisateur.ResponsableEntreprise, null, out _);
        var response = await client.GetAsync(url);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        Assert.DoesNotContain("SECRET", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reconciliation_cannot_link_a_foreign_invoice_or_transaction()
    {
        using var client = host.Connect(RoleUtilisateur.ResponsableFinancier, host.TenantA, out _);
        foreach (var transactionId in new[] { host.TransactionA.Id, host.TransactionB.Id })
        {
            var response = await client.PostAsJsonAsync("/api/lettrage", new { transactionId, factureId = host.QuoteB.Id });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        host.WithDb(db => Assert.All(db.Transactions, t => Assert.Null(t.FactureId)));
    }

    [Fact]
    public async Task Revoked_sessions_and_changed_roles_invalidate_existing_tokens()
    {
        using var revoked = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out var revokedId);
        host.WithDb(db => db.RefreshTokens.Single(t => t.UtilisateurId == revokedId).Revoquer());
        Assert.Equal(HttpStatusCode.Unauthorized, (await revoked.GetAsync("/api/factures")).StatusCode);
        using var changed = host.Connect(RoleUtilisateur.CollaborateurVentes, host.TenantA, out var changedId);
        host.WithDb(db => db.Utilisateurs.Single(u => u.Id == changedId).ChangerRole(RoleUtilisateur.ResponsableFinancier));
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/factures")).StatusCode);
    }
}
