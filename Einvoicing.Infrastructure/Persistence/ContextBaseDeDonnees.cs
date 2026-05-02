using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Einvoicing.Infrastructure.Persistence;

public sealed class ContextBaseDeDonnees(DbContextOptions<ContextBaseDeDonnees> options)
    : DbContext(options)
{
    
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<SessionActive> Sessions => Set<SessionActive>();

    
    public DbSet<Entreprise> Entreprises => Set<Entreprise>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<CategorieProduit> Categories => Set<CategorieProduit>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<ParametreFiscal> ParametresFiscaux => Set<ParametreFiscal>();
    public DbSet<Taxe> Taxes => Set<Taxe>();
    public DbSet<Personnalisation> Personnalisations => Set<Personnalisation>();

    
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<LigneFacture> LignesFacture => Set<LigneFacture>();
    public DbSet<HistoriqueFacture> HistoriqueFactures => Set<HistoriqueFacture>();
    public DbSet<CompteurFacture> CompteurFactures => Set<CompteurFacture>();

    
    public DbSet<Paiement> Paiements => Set<Paiement>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<SignatureRequest> Signatures => Set<SignatureRequest>();
    public DbSet<ExternalExchange> Echanges => Set<ExternalExchange>();
    public DbSet<DemoRequest> DemoRequests => Set<DemoRequest>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(ContextBaseDeDonnees).Assembly);
        base.OnModelCreating(mb);
    }
}





internal sealed class UtilisateurConfiguration : IEntityTypeConfiguration<Utilisateur>
{
    public void Configure(EntityTypeBuilder<Utilisateur> b)
    {
        b.ToTable("Utilisateurs");
        b.HasKey(u => u.Id);
        b.Property(u => u.Prenom).HasMaxLength(100).IsRequired();
        b.Property(u => u.Nom).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(254).IsRequired();
        b.Property(u => u.MotDePasseHash).HasMaxLength(512).IsRequired();
        b.Property(u => u.Telephone).HasMaxLength(20);
        b.Property(u => u.Poste).HasMaxLength(100);
        b.Property(u => u.Departement).HasMaxLength(100);
        b.Property(u => u.DeuxFASecret).HasMaxLength(64);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        b.Property(u => u.Statut).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasQueryFilter(u => !u.EstSupprime);
        b.HasMany(u => u.RefreshTokens).WithOne().HasForeignKey(r => r.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(u => u.OtpCodes).WithOne().HasForeignKey(o => o.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(u => u.Sessions).WithOne().HasForeignKey(s => s.UtilisateurId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens"); b.HasKey(r => r.Id);
        b.Property(r => r.Token).HasMaxLength(256).IsRequired();
        b.Property(r => r.JwtId).HasMaxLength(128).IsRequired();
        b.Property(r => r.AdresseIp).HasMaxLength(45);
        b.Property(r => r.UserAgent).HasMaxLength(512);
        b.HasIndex(r => r.Token); b.HasIndex(r => r.UtilisateurId);
    }
}

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.ToTable("OtpCodes"); b.HasKey(o => o.Id);
        b.Property(o => o.Code).HasMaxLength(6).IsRequired();
        b.Property(o => o.Type).HasConversion<string>().HasMaxLength(50);
        b.HasIndex(o => new { o.UtilisateurId, o.Type, o.EstUtilise, o.ExpireLe });
    }
}

internal sealed class SessionActiveConfiguration : IEntityTypeConfiguration<SessionActive>
{
    public void Configure(EntityTypeBuilder<SessionActive> b)
    {
        b.ToTable("Sessions"); b.HasKey(s => s.Id);
        b.Property(s => s.Appareil).HasMaxLength(200);
        b.Property(s => s.TypeAppareil).HasMaxLength(20);
        b.Property(s => s.UserAgent).HasMaxLength(512);
        b.Property(s => s.Localisation).HasMaxLength(100);
        b.Property(s => s.AdresseIp).HasMaxLength(45);
        b.Property(s => s.RefreshTokenRef).HasMaxLength(100);
        b.HasIndex(s => s.UtilisateurId);
    }
}





internal sealed class EntrepriseConfiguration : IEntityTypeConfiguration<Entreprise>
{
    public void Configure(EntityTypeBuilder<Entreprise> b)
    {
        b.ToTable("Entreprises"); b.HasKey(e => e.Id);
        b.Property(e => e.Nom).HasMaxLength(200).IsRequired();
        b.Property(e => e.MatriculeFiscal).HasMaxLength(13).IsRequired();
        b.Property(e => e.Adresse).HasMaxLength(300).IsRequired();
        b.Property(e => e.Ville).HasMaxLength(100).IsRequired();
        b.Property(e => e.CodePostal).HasMaxLength(10).IsRequired();
        b.Property(e => e.Pays).HasMaxLength(2).IsRequired();
        b.Property(e => e.Email).HasMaxLength(254).IsRequired();
        b.Property(e => e.Telephone).HasMaxLength(20);
        b.Property(e => e.SiteWeb).HasMaxLength(200);
        b.Property(e => e.LogoUrl).HasMaxLength(500);
        b.Property(e => e.CodeTva).HasMaxLength(50).IsRequired();
        b.Property(e => e.DevisePrincipale).HasMaxLength(3).HasDefaultValue("TND").IsRequired();
        b.Property(e => e.VersionTeif).HasMaxLength(10).IsRequired();
        b.Property(e => e.ParametresTeif).HasColumnType("text");
        b.Property(e => e.ScoreKyc).HasDefaultValue(0);
        b.Property(e => e.DonneesScoring).HasColumnType("text");
        b.Property(e => e.DocumentsUploades).HasColumnType("text");
        b.Property(e => e.DonneesInscription).HasColumnType("text");
        b.Property(e => e.RegimeFiscal).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(e => e.MatriculeFiscal).IsUnique();
    }
}

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("Clients"); b.HasKey(c => c.Id);
        b.Property(c => c.Nom).HasMaxLength(200).IsRequired();
        b.Property(c => c.Email).HasMaxLength(254).IsRequired();
        b.Property(c => c.MatriculeFiscal).HasMaxLength(13);
        b.Property(c => c.Adresse).HasMaxLength(300);
        b.Property(c => c.Ville).HasMaxLength(100);
        b.Property(c => c.CodePostal).HasMaxLength(10);
        b.Property(c => c.Pays).HasMaxLength(2).IsRequired();
        b.Property(c => c.Telephone).HasMaxLength(20);
        b.Property(c => c.TypeClient).HasConversion<string>().HasMaxLength(10);
        b.HasIndex(c => new { c.EntrepriseId, c.Email }).IsUnique();
        b.HasIndex(c => c.EntrepriseId);
    }
}

internal sealed class CategorieProduitConfiguration : IEntityTypeConfiguration<CategorieProduit>
{
    public void Configure(EntityTypeBuilder<CategorieProduit> b)
    {
        b.ToTable("Categories"); b.HasKey(c => c.Id);
        b.Property(c => c.Nom).HasMaxLength(100).IsRequired();
        b.Property(c => c.Description).HasMaxLength(500);
        b.HasIndex(c => c.EntrepriseId);
        b.HasMany(c => c.Produits).WithOne().HasForeignKey(p => p.CategorieId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProduitConfiguration : IEntityTypeConfiguration<Produit>
{
    public void Configure(EntityTypeBuilder<Produit> b)
    {
        b.ToTable("Produits"); b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(50).IsRequired();
        b.Property(p => p.Libelle).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.PrixUnitaire).HasColumnType("numeric(15,3)").IsRequired();
        b.Property(p => p.TauxTva).HasColumnType("numeric(5,2)").IsRequired();
        b.Property(p => p.Unite).HasMaxLength(20).IsRequired();
        b.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(p => new { p.EntrepriseId, p.Code }).IsUnique();
        b.HasIndex(p => p.EntrepriseId);
    }
}






internal sealed class TaxeConfiguration : IEntityTypeConfiguration<Taxe>
{
    public void Configure(EntityTypeBuilder<Taxe> b)
    {
        b.ToTable("Taxes"); b.HasKey(t => t.Id);
        b.Property(t => t.Titre).HasMaxLength(200).IsRequired();
        b.Property(t => t.Taux).HasColumnType("numeric(5,2)").IsRequired();
        b.Property(t => t.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(t => t.Description).HasMaxLength(1000);
        b.HasIndex(t => t.EntrepriseId);
    }
}
internal sealed class ParametreFiscalConfiguration : IEntityTypeConfiguration<ParametreFiscal>
{
    public void Configure(EntityTypeBuilder<ParametreFiscal> b)
    {
        b.ToTable("ParametresFiscaux"); b.HasKey(p => p.Id);
        b.Property(p => p.Libelle).HasMaxLength(200).IsRequired();
        b.Property(p => p.Valeur).HasColumnType("numeric(15,3)").IsRequired();
        b.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Signe).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.OrdreCalcul).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Utilisation).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.InclureRetenueSource).HasDefaultValue(false);
        b.Property(p => p.EstActif).HasDefaultValue(true);

        var converter = new ValueConverter<List<string>, string>(
            v => string.Join('|', v ?? new List<string>()),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<string>()
                : v.Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList()
        );
        var comparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 ?? new List<string>()).SequenceEqual(c2 ?? new List<string>()),
            c => (c ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
            c => (c ?? new List<string>()).ToList()
        );

        b.Property(p => p.DocumentsCibles)
            .HasConversion(converter)
            .Metadata.SetValueComparer(comparer);
        b.Property(p => p.DocumentsCibles).HasColumnType("text");
        b.HasIndex(p => p.EntrepriseId);
    }
}
internal sealed class PersonnalisationConfiguration : IEntityTypeConfiguration<Personnalisation>
{
    public void Configure(EntityTypeBuilder<Personnalisation> b)
    {
        b.ToTable("Personnalisations"); b.HasKey(p => p.Id);
        b.Property(p => p.DonneesJson).HasColumnType("text").IsRequired();
        b.HasIndex(p => p.EntrepriseId).IsUnique();
    }
}
internal sealed class FactureConfiguration : IEntityTypeConfiguration<Facture>
{
    public void Configure(EntityTypeBuilder<Facture> b)
    {
        b.ToTable("Factures"); b.HasKey(f => f.Id);
        b.Property(f => f.Numero).HasMaxLength(50).IsRequired();
        b.Property(f => f.Reference).HasMaxLength(100);
        b.Property(f => f.Statut).HasConversion<string>().HasMaxLength(30);
        b.Property(f => f.TypeFacture).HasConversion<string>().HasMaxLength(20);
        b.Property(f => f.ModePaiement).HasConversion<string>().HasMaxLength(20);
        b.Property(f => f.Devise).HasMaxLength(3).IsRequired();
        b.Property(f => f.TotalHt).HasColumnType("numeric(15,3)");
        b.Property(f => f.TotalTva).HasColumnType("numeric(15,3)");
        b.Property(f => f.TotalTtc).HasColumnType("numeric(15,3)");
        b.Property(f => f.MontantPaye).HasColumnType("numeric(15,3)");
        b.Property(f => f.Notes).HasMaxLength(1000);
        b.Property(f => f.ConditionsPaiement).HasMaxLength(500);
        b.Property(f => f.XmlTeif).HasColumnType("text");
        b.Property(f => f.HashIntegrite).HasMaxLength(64);
        b.Property(f => f.VersionTeif).HasMaxLength(10);
        b.HasIndex(f => new { f.EntrepriseId, f.Numero }).IsUnique();
        b.HasIndex(f => f.EntrepriseId);
        b.HasIndex(f => f.ClientId);
        b.HasIndex(f => f.Statut);
        b.HasMany(f => f.Lignes).WithOne().HasForeignKey(l => l.FactureId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(f => f.Historique).WithOne().HasForeignKey(h => h.FactureId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LigneFactureConfiguration : IEntityTypeConfiguration<LigneFacture>
{
    public void Configure(EntityTypeBuilder<LigneFacture> b)
    {
        b.ToTable("LignesFacture"); b.HasKey(l => l.Id);
        b.Property(l => l.Designation).HasMaxLength(200).IsRequired();
        b.Property(l => l.Description).HasMaxLength(1000);
        b.Property(l => l.Unite).HasMaxLength(20).IsRequired();
        b.Property(l => l.Quantite).HasColumnType("numeric(15,3)");
        b.Property(l => l.PrixUnitaire).HasColumnType("numeric(15,3)");
        b.Property(l => l.TauxRemise).HasColumnType("numeric(5,2)");
        b.Property(l => l.TauxTva).HasColumnType("numeric(5,2)");
        b.Property(l => l.MontantHt).HasColumnType("numeric(15,3)");
        b.Property(l => l.MontantRemise).HasColumnType("numeric(15,3)");
        b.Property(l => l.MontantTva).HasColumnType("numeric(15,3)");
        b.Property(l => l.MontantTtc).HasColumnType("numeric(15,3)");
        b.HasIndex(l => l.FactureId);
    }
}

internal sealed class HistoriqueFactureConfiguration : IEntityTypeConfiguration<HistoriqueFacture>
{
    public void Configure(EntityTypeBuilder<HistoriqueFacture> b)
    {
        b.ToTable("HistoriqueFactures"); b.HasKey(h => h.Id);
        b.Property(h => h.Action).HasMaxLength(100).IsRequired();
        b.Property(h => h.Details).HasMaxLength(2000).IsRequired();
        b.Property(h => h.AncienneValeur).HasMaxLength(500);
        b.Property(h => h.NouvelleValeur).HasMaxLength(500);
        b.HasIndex(h => h.FactureId);
    }
}

internal sealed class CompteurFactureConfiguration : IEntityTypeConfiguration<CompteurFacture>
{
    public void Configure(EntityTypeBuilder<CompteurFacture> b)
    {
        b.ToTable("CompteurFactures"); b.HasKey(c => c.Id);
        b.Property(c => c.Prefixe).HasMaxLength(10).IsRequired();
        b.HasIndex(c => new { c.EntrepriseId, c.Annee, c.Mois }).IsUnique();
    }
}





internal sealed class PaiementConfiguration : IEntityTypeConfiguration<Paiement>
{
    public void Configure(EntityTypeBuilder<Paiement> b)
    {
        b.ToTable("Paiements"); b.HasKey(p => p.Id);
        b.Property(p => p.Montant).HasColumnType("numeric(15,3)").IsRequired();
        b.Property(p => p.Devise).HasMaxLength(3).IsRequired();
        b.Property(p => p.Mode).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Reference).HasMaxLength(100);
        b.Property(p => p.Banque).HasMaxLength(100);
        b.Property(p => p.Notes).HasMaxLength(500);
        b.HasIndex(p => p.FactureId);
        b.HasIndex(p => p.EntrepriseId);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("Transactions"); b.HasKey(t => t.Id);
        b.Property(t => t.Libelle).HasMaxLength(250).IsRequired();
        b.Property(t => t.Description).HasMaxLength(1500);
        b.Property(t => t.TiersNom).HasMaxLength(200);
        b.Property(t => t.CategorieNom).HasMaxLength(120);
        b.Property(t => t.Compte).HasMaxLength(120);

        b.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.Statut).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.StatutJustificatif).HasConversion<string>().HasMaxLength(20);

        b.Property(t => t.Devise).HasMaxLength(3).IsRequired();
        b.Property(t => t.Montant).HasColumnType("numeric(15,3)").IsRequired();

        b.Property(t => t.JustificatifChemin).HasMaxLength(600);
        b.Property(t => t.JustificatifNomFichier).HasMaxLength(260);
        b.Property(t => t.JustificatifContentType).HasMaxLength(100);

        b.HasIndex(t => t.EntrepriseId);
        b.HasIndex(t => new { t.EntrepriseId, t.Date });
        b.HasIndex(t => new { t.EntrepriseId, t.Statut });
        b.HasIndex(t => t.FactureId);
    }
}

internal sealed class SignatureConfiguration : IEntityTypeConfiguration<SignatureRequest>
{
    public void Configure(EntityTypeBuilder<SignatureRequest> b)
    {
        b.ToTable("Signatures"); b.HasKey(s => s.Id);
        b.Property(s => s.Statut).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.SignatureValue).HasColumnType("text");
        b.Property(s => s.CertificatId).HasMaxLength(100);
        b.Property(s => s.MessageErreur).HasMaxLength(1000);
        b.HasIndex(s => s.FactureId);
        b.HasIndex(s => s.EntrepriseId);
    }
}

internal sealed class ExchangeConfiguration : IEntityTypeConfiguration<ExternalExchange>
{
    public void Configure(EntityTypeBuilder<ExternalExchange> b)
    {
        b.ToTable("Echanges"); b.HasKey(e => e.Id);
        b.Property(e => e.CorrelationId).HasMaxLength(36).IsRequired();
        b.Property(e => e.Statut).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.ReponseCode).HasMaxLength(20);
        b.Property(e => e.ReponseMessage).HasMaxLength(1000);
        b.Property(e => e.MotifRejet).HasMaxLength(500);
        b.HasIndex(e => e.FactureId);
        b.HasIndex(e => e.EntrepriseId);
        b.HasIndex(e => e.CorrelationId).IsUnique();
    }
}

internal sealed class DemoRequestConfiguration : IEntityTypeConfiguration<DemoRequest>
{
    public void Configure(EntityTypeBuilder<DemoRequest> b)
    {
        b.ToTable("DemoRequests"); b.HasKey(d => d.Id);
        b.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        b.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        b.Property(d => d.Email).HasMaxLength(254).IsRequired();
        b.Property(d => d.Company).HasMaxLength(200).IsRequired();
        b.Property(d => d.Phone).HasMaxLength(20);
        b.Property(d => d.Message).HasMaxLength(1000);
        b.Property(d => d.PreferredTime).HasMaxLength(10).IsRequired();
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(d => d.Email);
        b.HasIndex(d => d.Status);
        b.HasIndex(d => d.CreatedAt);
    }
}
