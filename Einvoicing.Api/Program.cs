using Einvoicing.Api.Middleware;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Application.Options;
using Einvoicing.Api.Services;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Repositories;
using Einvoicing.Infrastructure.Services;
using Einvoicing.Infrastructure.Seed;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ContextBaseDeDonnees>(opts =>
    opts.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly("Einvoicing.Infrastructure")
    ));

var jwtSection = builder.Configuration.GetSection("Jwt");
var secret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret manquant dans appsettings.json");

builder.Services
    .AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opts.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        opts.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers.Append("Token-Expired", "true");
                return Task.CompletedTask;
            }
        };
    });

var allowAll = new AuthorizationPolicyBuilder()
    .RequireAssertion(_ => true)
    .Build();

var authenticatedByDefault = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();

builder.Services.AddAuthorization(options =>
{
    // En d?veloppement on garde un fallback permissif pour fluidifier l'int?gration.
    // En production, tout endpoint non d?cor? explicitement devient authentifi? par d?faut.
    var baselinePolicy = builder.Environment.IsProduction() ? authenticatedByDefault : allowAll;

    options.DefaultPolicy = baselinePolicy;
    options.FallbackPolicy = baselinePolicy;
    options.AddPolicy("SuperAdmin", p => p.RequireRole("SuperAdmin"));
    options.AddPolicy("Admin", p => p.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("SuperOuAdmin", p => p.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("ResponsableOuAdmin", p => p.RequireRole("SuperAdmin", "Admin", "ResponsableEntreprise"));
    options.AddPolicy("Tous", p => p.RequireRole("SuperAdmin", "Admin", "ResponsableEntreprise", "ResponsableFinancier"));
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDemandeAccesService, DemandeAccesService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<KycScoringService>();
builder.Services.AddHttpClient<IOcrClient, OcrClient>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("OcrService:TimeoutSeconds", 360);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});


builder.Services.AddScoped<IEntrepriseService, EntrepriseService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ICategorieService, CategorieService>();
builder.Services.AddScoped<IProduitService, ProduitService>();
builder.Services.AddScoped<IParametreFiscalService, ParametreFiscalService>();
builder.Services.AddScoped<ITaxeService, TaxeService>();
builder.Services.AddScoped<IPersonnalisationService, PersonnalisationService>();
builder.Services.AddScoped<IUtilisateurService, UtilisateurService>();

builder.Services.AddScoped<IFactureService, FactureService>();
builder.Services.AddScoped<ITeifService, TeifService>();
builder.Services.AddScoped<INumeroFactureService, NumeroFactureService>();

builder.Services.AddScoped<IPaiementService, PaiementService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IScanService, ScanService>();
builder.Services.AddScoped<IFournisseurService, FournisseurService>();
builder.Services.AddScoped<IExpenseReviewService, ExpenseReviewService>();
builder.Services.AddScoped<ISignatureService, SignatureService>();
builder.Services.AddScoped<IEchangeTtnService, EchangeTtnService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IRapportService, RapportService>();
builder.Services.AddScoped<IRelanceService, RelanceService>();
builder.Services.AddScoped<ISignatureProvider, MockSignatureProvider>();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();

builder.Services.Configure<RelanceOptions>(builder.Configuration.GetSection("Relances"));
builder.Services.AddHostedService<RelanceBackgroundService>();

builder.Services.AddScoped<IUtilisateurRepository, UtilisateurRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();

builder.Services.AddScoped<IEntrepriseRepository, EntrepriseRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<ICategorieRepository, CategorieRepository>();
builder.Services.AddScoped<IProduitRepository, ProduitRepository>();
builder.Services.AddScoped<IParametreFiscalRepository, ParametreFiscalRepository>();
builder.Services.AddScoped<ITaxeRepository, TaxeRepository>();
builder.Services.AddScoped<IPersonnalisationRepository, PersonnalisationRepository>();

builder.Services.AddScoped<IFactureRepository, FactureRepository>();
builder.Services.AddScoped<ICompteurFactureRepository, CompteurFactureRepository>();

builder.Services.AddScoped<IPaiementRepository, PaiementRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IScannedDocumentRepository, ScannedDocumentRepository>();
builder.Services.AddScoped<IFournisseurRepository, FournisseurRepository>();
builder.Services.AddScoped<ISignatureRepository, SignatureRepository>();
builder.Services.AddScoped<IEchangeRepository, EchangeRepository>();
builder.Services.AddScoped<IDemoRequestRepository, DemoRequestRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Einvoicing API",
        Version = "v1",
        Description = "Portail de facturation electronique - DGI Tunisie - TEIF 2024 - EN16931"
    });
    opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer {token}"
    });
    opts.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }});
});

builder.Services.AddCors(opts =>
    opts.AddPolicy("Angular", p =>
        p.WithOrigins("http://localhost:4200", "https://invoice.ey.tn")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DataSeeder.SeedAsync(services);
}


// Dev only: auto-start local OCR microservice if missing.
await OcrServiceDevLauncher.EnsureRunningAsync(app.Configuration, app.Environment, app.Logger, app.Lifetime.ApplicationStopping);

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("Angular");

var uploadsRoot = app.Configuration["Uploads:Root"];
if (string.IsNullOrWhiteSpace(uploadsRoot))
{
    uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
}
else if (!Path.IsPathRooted(uploadsRoot))
{
    uploadsRoot = Path.Combine(app.Environment.ContentRootPath, uploadsRoot);
}
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

// HTTPS reste d?sactiv? en d?veloppement local pour ?viter les frictions de certificat,
// mais doit ?tre actif d?s qu'on sort du mode Development.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Einvoicing API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
