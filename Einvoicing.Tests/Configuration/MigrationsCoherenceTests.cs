using Einvoicing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Einvoicing.Tests.Configuration;

/// <summary>
/// Garantit que le snapshot des migrations correspond au modèle EF (aucune migration oubliée)
/// et que la migration du timbre fiscal / de la remise globale est bien enregistrée.
/// Aucune connexion à la base n'est ouverte.
/// </summary>
public sealed class MigrationsCoherenceTests
{
    private static ContextBaseDeDonnees CreerContexte() =>
        new(new DbContextOptionsBuilder<ContextBaseDeDonnees>()
            .UseNpgsql("Host=localhost;Database=verification_modele")
            .Options);

    [Fact]
    public void LeSnapshotDesMigrations_CorrespondAuModele()
    {
        using var ctx = CreerContexte();
        var migrationsAssembly = ctx.GetService<IMigrationsAssembly>();
        var differ = ctx.GetService<IMigrationsModelDiffer>();
        var initializer = ctx.GetService<IModelRuntimeInitializer>();

        var snapshot = migrationsAssembly.ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = initializer.Initialize(snapshot);

        var differences = differ.GetDifferences(
            snapshot.GetRelationalModel(),
            ctx.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        differences.Should().BeEmpty(
            "toute modification du modèle doit être accompagnée d'une migration ({0})",
            string.Join(", ", differences.Select(d => d.GetType().Name)));
    }

    [Fact]
    public void LaMigrationTimbreRemise_EstEnregistree()
    {
        using var ctx = CreerContexte();
        ctx.Database.GetMigrations().Should().Contain("20261117090000_AddTimbreRemiseFacture");
    }
}

public sealed class DatesUtcTests
{
    [Fact]
    public void LesDatesSontNormaliseesEnUtcAvantEcriture()
    {
        using var ctx = new ContextBaseDeDonnees(new DbContextOptionsBuilder<ContextBaseDeDonnees>()
            .UseNpgsql("Host=localhost;Database=verification_modele").Options);
        var propriete = ctx.Model.FindEntityType(typeof(Einvoicing.Domain.Entities.Facture))!.FindProperty("DateEcheance")!;
        var conv = propriete.GetValueConverter()!;

        var locale = new DateTime(2026, 12, 17, 10, 0, 0, DateTimeKind.Local);
        ((DateTime)conv.ConvertToProvider(locale)!).Kind.Should().Be(DateTimeKind.Utc);
        ((DateTime)conv.ConvertToProvider(locale)!).Should().Be(locale.ToUniversalTime());

        var sansFuseau = new DateTime(2026, 12, 17);
        var utc = (DateTime)conv.ConvertToProvider(sansFuseau)!;
        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Should().Be(new DateTime(2026, 12, 17, 0, 0, 0, DateTimeKind.Utc));
    }
}
