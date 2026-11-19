using Einvoicing.Application.Configuration;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Persistence;
using Einvoicing.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Tests.Configuration;

public sealed class PlatformConfigurationServiceTests
{
    [Fact]
    public async Task UpdateAsync_PersistsValueAndHistory()
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);
        var actorId = Guid.NewGuid();

        var updated = await service.UpdateAsync(
            PlatformConfigurationKeys.KycAutoApprovalThreshold,
            "85",
            "Ajustement après revue des dossiers KYC.",
            actorId);

        updated.Value.Should().Be("85");
        db.HistoriqueParametresPlateforme.Should().ContainSingle(x =>
            x.Cle == PlatformConfigurationKeys.KycAutoApprovalThreshold
            && x.AncienneValeur == "80"
            && x.NouvelleValeur == "85"
            && x.ModifiePar == actorId);
    }

    [Fact]
    public async Task UpdateAsync_RejectsLockedParameter()
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);
        var actorId = Guid.NewGuid();
        await service.SetLockAsync(
            PlatformConfigurationKeys.KycAutoApprovalThreshold,
            true,
            "Gel temporaire pendant la recette.",
            actorId);

        var action = () => service.UpdateAsync(
            PlatformConfigurationKeys.KycAutoApprovalThreshold,
            "85",
            "Tentative de modification.",
            actorId);

        await action.Should().ThrowAsync<ValidationMetierException>()
            .WithMessage("*verrouillé*");
    }

    [Fact]
    public async Task UpdateAsync_RejectsInconsistentThresholds()
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);

        var action = () => service.UpdateAsync(
            PlatformConfigurationKeys.KycManualReviewThreshold,
            "80",
            "Test de cohérence.",
            Guid.NewGuid());

        await action.Should().ThrowAsync<ValidationMetierException>()
            .WithMessage("*inférieur*");
    }

    [Fact]
    public async Task Defaults_AreListedAndUnknownSettingUsesFallback()
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);

        var settings = await service.ListAsync();

        settings.Should().ContainSingle(x => x.Key == PlatformConfigurationKeys.KycAutoApprovalThreshold && x.Value == "80");
        settings.Should().ContainSingle(x => x.Key == PlatformConfigurationKeys.KycManualReviewThreshold && x.Value == "50");
        (await service.GetIntAsync(PlatformConfigurationKeys.KycAutoApprovalThreshold, -1)).Should().Be(80);
        (await service.GetIntAsync("unknown", -1)).Should().Be(-1);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("80")]
    [InlineData("50")]
    public async Task UpdateAsync_RejectsInvalidOrUnchangedApprovalThresholdWithoutAuditEntry(string value)
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);

        var update = () => service.UpdateAsync(PlatformConfigurationKeys.KycAutoApprovalThreshold, value, "Test KYC", Guid.NewGuid());

        await update.Should().ThrowAsync<ValidationMetierException>();
        (await service.GetIntAsync(PlatformConfigurationKeys.KycAutoApprovalThreshold, -1)).Should().Be(80);
        (await service.GetHistoryAsync(PlatformConfigurationKeys.KycAutoApprovalThreshold)).Should().BeEmpty();
    }

    [Fact]
    public async Task SetLockAsync_RecordsBothTransitionsAndAllowsUpdatingAfterUnlock()
    {
        await using var db = CreateDb();
        var service = new PlatformConfigurationService(db);
        var key = PlatformConfigurationKeys.KycManualReviewThreshold;
        var actor = Guid.NewGuid();

        await service.SetLockAsync(key, true, "Freeze", actor);
        var duplicate = () => service.SetLockAsync(key, true, "Freeze again", actor);
        await duplicate.Should().ThrowAsync<ValidationMetierException>();
        await service.SetLockAsync(key, false, "Unfreeze", actor);
        var updated = await service.UpdateAsync(key, " 055 ", "Adjust threshold", actor);

        updated.Value.Should().Be("55");
        var history = await service.GetHistoryAsync(key);
        history.Select(x => x.Action).Should().BeEquivalentTo(new[] { "Verrouillage", "D\u00e9verrouillage", "Modification" });
    }

    private static ContextBaseDeDonnees CreateDb()
    {
        var options = new DbContextOptionsBuilder<ContextBaseDeDonnees>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ContextBaseDeDonnees(options);
    }
}
