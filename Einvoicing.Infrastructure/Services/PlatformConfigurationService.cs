using System.Globalization;
using Einvoicing.Application.Configuration;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Services;

public sealed class PlatformConfigurationService(ContextBaseDeDonnees db) : IPlatformConfigurationService
{
    private const string LegacySettingsKey = "admin-platform-settings";

    public async Task<IReadOnlyList<PlatformConfigurationDto>> ListAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var parameters = await db.ParametresPlateforme.AsNoTracking()
            // Les marqueurs techniques internes (ex. historique de démonstration) ne sont pas des réglages.
            .Where(x => x.Cle != LegacySettingsKey && !x.Cle.StartsWith("demo."))
            .OrderBy(x => x.Cle)
            .ToListAsync(ct);
        return parameters.Select(ToDto).ToList();
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var raw = await db.ParametresPlateforme.AsNoTracking()
            .Where(x => x.Cle == key)
            .Select(x => x.ValeurJson)
            .SingleOrDefaultAsync(ct);

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    public async Task<PlatformConfigurationDto> UpdateAsync(
        string key,
        string value,
        string reason,
        Guid actorId,
        CancellationToken ct = default)
    {
        ValidateReason(reason);
        await EnsureDefaultsAsync(ct);

        var parameter = await FindAsync(key, ct);
        var normalizedValue = await ValidateAndNormalizeValueAsync(parameter.Cle, value, ct);
        if (parameter.ValeurJson == normalizedValue)
            throw new ValidationMetierException("La nouvelle valeur est identique à la valeur actuelle.");

        var history = HistoriqueParametrePlateforme.Creer(
            parameter,
            parameter.ValeurJson,
            normalizedValue,
            "Modification",
            reason,
            actorId);

        parameter.MettreAJour(normalizedValue, actorId);
        db.HistoriqueParametresPlateforme.Add(history);
        await db.SaveChangesAsync(ct);
        return ToDto(parameter);
    }

    public async Task<PlatformConfigurationDto> SetLockAsync(
        string key,
        bool locked,
        string reason,
        Guid actorId,
        CancellationToken ct = default)
    {
        ValidateReason(reason);
        await EnsureDefaultsAsync(ct);

        var parameter = await FindAsync(key, ct);
        if (parameter.Verrouille == locked)
            throw new ValidationMetierException(locked
                ? "Ce paramètre est déjà verrouillé."
                : "Ce paramètre est déjà déverrouillé.");

        var history = HistoriqueParametrePlateforme.Creer(
            parameter,
            parameter.Verrouille.ToString(),
            locked.ToString(),
            locked ? "Verrouillage" : "Déverrouillage",
            reason,
            actorId);

        parameter.DefinirVerrouillage(locked, actorId);
        db.HistoriqueParametresPlateforme.Add(history);
        await db.SaveChangesAsync(ct);
        return ToDto(parameter);
    }

    public async Task<IReadOnlyList<PlatformConfigurationHistoryDto>> GetHistoryAsync(
        string key,
        CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        _ = await FindAsync(key, ct);

        return await (
            from history in db.HistoriqueParametresPlateforme.AsNoTracking()
            join user in db.Utilisateurs.AsNoTracking() on history.ModifiePar equals user.Id into users
            from user in users.DefaultIfEmpty()
            where history.Cle == key
            orderby history.ModifieLe descending
            select new PlatformConfigurationHistoryDto(
                history.Id,
                history.Cle,
                history.AncienneValeur,
                history.NouvelleValeur,
                history.Action,
                history.Motif,
                history.ModifiePar,
                user == null ? "Utilisateur supprimé" : user.Prenom + " " + user.Nom,
                history.ModifieLe))
            .ToListAsync(ct);
    }

    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        var keys = await db.ParametresPlateforme
            .Where(x => x.Cle == PlatformConfigurationKeys.KycAutoApprovalThreshold
                || x.Cle == PlatformConfigurationKeys.KycManualReviewThreshold)
            .Select(x => x.Cle)
            .ToListAsync(ct);

        if (!keys.Contains(PlatformConfigurationKeys.KycAutoApprovalThreshold))
        {
            db.ParametresPlateforme.Add(ParametrePlateforme.Creer(
                PlatformConfigurationKeys.KycAutoApprovalThreshold,
                "80",
                "Entier",
                "Score minimal sans anomalie critique permettant de proposer l'approbation automatique d'un dossier KYC."));
        }

        if (!keys.Contains(PlatformConfigurationKeys.KycManualReviewThreshold))
        {
            db.ParametresPlateforme.Add(ParametrePlateforme.Creer(
                PlatformConfigurationKeys.KycManualReviewThreshold,
                "50",
                "Entier",
                "Score minimal orientant un dossier KYC vers une revue manuelle plutôt que vers un rejet automatique."));
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    private async Task<ParametrePlateforme> FindAsync(string key, CancellationToken ct)
        => await db.ParametresPlateforme.SingleOrDefaultAsync(x => x.Cle == key, ct)
            ?? throw new NotFoundException("Paramètre de plateforme introuvable.");

    private async Task<string> ValidateAndNormalizeValueAsync(string key, string rawValue, CancellationToken ct)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new ValidationMetierException("La valeur doit être un nombre entier.");

        if (value is < 0 or > 100)
            throw new ValidationMetierException("Le seuil doit être compris entre 0 et 100.");

        if (key == PlatformConfigurationKeys.KycAutoApprovalThreshold)
        {
            var manualThreshold = await GetStoredIntAsync(PlatformConfigurationKeys.KycManualReviewThreshold, 50, ct);
            if (value <= manualThreshold)
                throw new ValidationMetierException("Le seuil d'approbation doit être supérieur au seuil de revue manuelle.");
        }
        else if (key == PlatformConfigurationKeys.KycManualReviewThreshold)
        {
            var approvalThreshold = await GetStoredIntAsync(PlatformConfigurationKeys.KycAutoApprovalThreshold, 80, ct);
            if (value >= approvalThreshold)
                throw new ValidationMetierException("Le seuil de revue manuelle doit être inférieur au seuil d'approbation.");
        }
        else
        {
            throw new ValidationMetierException("Ce paramètre n'est pas modifiable par ce service.");
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<int> GetStoredIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var raw = await db.ParametresPlateforme.AsNoTracking()
            .Where(x => x.Cle == key)
            .Select(x => x.ValeurJson)
            .SingleOrDefaultAsync(ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationMetierException("Le motif de la modification est obligatoire.");
        if (reason.Trim().Length > 500)
            throw new ValidationMetierException("Le motif ne peut pas dépasser 500 caractères.");
    }

    private static PlatformConfigurationDto ToDto(ParametrePlateforme parameter)
        => new(
            parameter.Id,
            parameter.Cle,
            parameter.ValeurJson,
            parameter.TypeValeur,
            parameter.Description,
            parameter.Verrouille,
            parameter.ModifiePar,
            parameter.ModifieLe);
}
