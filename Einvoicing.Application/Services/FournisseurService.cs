using System.Globalization;
using System.Text;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class FournisseurService(IFournisseurRepository fournisseurRepo) : IFournisseurService
{
    private sealed record ScoredSupplier(Fournisseur Supplier, int Score);

    public async Task<FournisseurMatchResultDto> MatchOrCreateAsync(
        Guid entrepriseId,
        MatchOrCreateFournisseurRequest request,
        CancellationToken ct = default)
    {
        if (request.SelectedSupplierId.HasValue)
        {
            var selected = await fournisseurRepo.ObtenirParIdAsync(request.SelectedSupplierId.Value, ct)
                ?? throw new NotFoundException("Fournisseur introuvable.");
            if (selected.EntrepriseId != entrepriseId)
                throw new AccesRefuseException();

            return BuildResult(selected, matched: true, autoSelected: false, created: false, matchType: "selected", []);
        }

        var name = Clean(request.Nom);
        var taxId = Clean(request.MatriculeFiscal)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(taxId))
            throw new ValidationMetierException("Le nom ou le matricule fiscal du fournisseur est requis.");

        if (!string.IsNullOrWhiteSpace(taxId))
        {
            var exact = await fournisseurRepo.ObtenirParMatriculeFiscalAsync(entrepriseId, taxId, ct);
            if (exact is not null)
                return BuildResult(exact, matched: true, autoSelected: true, created: false, matchType: "tax-id", []);
        }

        var candidates = string.IsNullOrWhiteSpace(name)
            ? []
            : await fournisseurRepo.RechercherAsync(entrepriseId, name, ct);

        var scored = candidates
            .Select(x => new ScoredSupplier(x, ComputeScore(name, taxId, x)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count > 0)
        {
            var top = scored[0];
            if (top.Score >= 92)
                return BuildResult(top.Supplier, matched: true, autoSelected: true, created: false, matchType: "name-exact", scored);

            return new FournisseurMatchResultDto(
                Matched: true,
                AutoSelected: false,
                Created: false,
                MatchType: "name-similar",
                FournisseurId: null,
                DisplayName: name,
                MatriculeFiscal: taxId,
                Candidates: scored.Select(x => ToCandidate(x.Supplier, x.Score)).ToList());
        }

        if (request.CreateIfMissing)
        {
            var created = Fournisseur.Creer(
                entrepriseId,
                name ?? taxId ?? "Fournisseur scanné",
                taxId,
                request.Adresse,
                request.Iban);
            await fournisseurRepo.AjouterAsync(created, ct);
            await fournisseurRepo.SauvegarderAsync(ct);
            return BuildResult(created, matched: false, autoSelected: true, created: true, matchType: "created", []);
        }

        return new FournisseurMatchResultDto(
            Matched: false,
            AutoSelected: false,
            Created: false,
            MatchType: "not-found",
            FournisseurId: null,
            DisplayName: name,
            MatriculeFiscal: taxId,
            Candidates: []);
    }

    public async Task<IReadOnlyList<FournisseurDto>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var list = await fournisseurRepo.RechercherAsync(entrepriseId, terme, ct);
        return list.Select(Map).ToList();
    }

    public async Task<FournisseurDto?> ObtenirParIdAsync(Guid entrepriseId, Guid id, CancellationToken ct = default)
    {
        var supplier = await fournisseurRepo.ObtenirParIdAsync(id, ct);
        if (supplier is null || supplier.EntrepriseId != entrepriseId)
            return null;
        return Map(supplier);
    }

    private static FournisseurDto Map(Fournisseur x) => new(
        x.Id,
        x.EntrepriseId,
        x.Nom,
        x.MatriculeFiscal,
        x.Adresse,
        x.Iban,
        x.Email,
        x.Telephone,
        x.EstActif,
        x.CreeLe,
        x.ModifieLe);

    private static FournisseurMatchResultDto BuildResult(
        Fournisseur supplier,
        bool matched,
        bool autoSelected,
        bool created,
        string matchType,
        IEnumerable<ScoredSupplier> scored)
        => new(
            matched,
            autoSelected,
            created,
            matchType,
            supplier.Id,
            supplier.Nom,
            supplier.MatriculeFiscal,
            scored.Select(x => ToCandidate(x.Supplier, x.Score)).ToList());

    private static FournisseurMatchCandidateDto ToCandidate(Fournisseur supplier, int score)
        => new(
            supplier.Id,
            supplier.Nom,
            supplier.MatriculeFiscal,
            supplier.Adresse,
            supplier.Iban,
            score,
            score >= 92);

    private static int ComputeScore(string? name, string? taxId, Fournisseur supplier)
    {
        if (!string.IsNullOrWhiteSpace(taxId) && supplier.MatriculeFiscal == taxId)
            return 100;

        var normalizedInput = Normalize(name);
        var normalizedSupplier = Normalize(supplier.Nom);
        if (string.IsNullOrWhiteSpace(normalizedInput) || string.IsNullOrWhiteSpace(normalizedSupplier))
            return 0;
        if (normalizedInput == normalizedSupplier)
            return 95;
        if (normalizedSupplier.StartsWith(normalizedInput) || normalizedInput.StartsWith(normalizedSupplier))
            return 88;
        if (normalizedSupplier.Contains(normalizedInput) || normalizedInput.Contains(normalizedSupplier))
            return 80;

        var inputTokens = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var supplierTokens = normalizedSupplier.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var overlap = inputTokens.Intersect(supplierTokens).Count();
        if (overlap == 0)
            return 0;

        var ratio = (decimal)overlap / Math.Max(inputTokens.Length, supplierTokens.Length);
        return ratio switch
        {
            >= 0.75m => 78,
            >= 0.50m => 72,
            _ => 0
        };
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var formD = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ',
            builder.ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
