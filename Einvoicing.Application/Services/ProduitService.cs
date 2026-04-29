




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class ProduitService(
    IProduitRepository produitRepo,
    ICategorieRepository categorieRepo
) : IProduitService
{
    public async Task<ProduitDto> CreerAsync(
        Guid entrepriseId, CreerProduitRequest req, CancellationToken ct = default)
    {
        var existant = await produitRepo.ObtenirParCodeAsync(entrepriseId, req.Code, ct);
        if (existant is not null)
            throw new ConflitException($"Un produit avec le code '{req.Code}' existe déjà.");

        if (req.CategorieId.HasValue)
        {
            var cat = await categorieRepo.ObtenirParIdAsync(req.CategorieId.Value, ct);
            if (cat is null || cat.EntrepriseId != entrepriseId)
                throw new NotFoundException("Catégorie introuvable.");
        }

        var produit = Produit.Creer(
            entrepriseId, req.Code, req.Libelle, req.PrixUnitaire,
            req.TauxTva, req.Type, req.CategorieId, req.Description, req.Unite);

        await produitRepo.AjouterAsync(produit, ct);
        await produitRepo.SauvegarderAsync(ct);
        return await MapToDtoAsync(produit, ct);
    }

    public async Task<ProduitDto> ObtenirParIdAsync(
        Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var produit = await produitRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Produit introuvable.");
        if (produit.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        return await MapToDtoAsync(produit, ct);
    }

    public async Task<ListeProduitsDto> ListerAsync(
        Guid entrepriseId, int page, int parPage, Guid? categorieId, bool? actifSeulement,
        CancellationToken ct = default)
    {
        var (items, total) = await produitRepo.ListerAsync(
            entrepriseId, page, parPage, categorieId, actifSeulement, ct);
        var dtos = new List<ProduitDto>();
        foreach (var p in items) dtos.Add(await MapToDtoAsync(p, ct));
        return new ListeProduitsDto(dtos, total, page, parPage);
    }

    public async Task<ProduitDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, MettreAJourProduitRequest req, CancellationToken ct = default)
    {
        var produit = await produitRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Produit introuvable.");
        if (produit.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        produit.MettreAJour(req.Libelle, req.PrixUnitaire, req.TauxTva,
            req.Description, req.Unite, req.CategorieId, req.Type);

        produitRepo.MettreAJour(produit);
        await produitRepo.SauvegarderAsync(ct);
        return await MapToDtoAsync(produit, ct);
    }

    public async Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var produit = await produitRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Produit introuvable.");
        if (produit.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        produit.Desactiver();
        produitRepo.MettreAJour(produit);
        await produitRepo.SauvegarderAsync(ct);
    }

    public async Task ReactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var produit = await produitRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Produit introuvable.");
        if (produit.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        produit.Reactiver();
        produitRepo.MettreAJour(produit);
        await produitRepo.SauvegarderAsync(ct);
    }

    public async Task<IReadOnlyList<ProduitDto>> RechercherAsync(
        Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var liste = await produitRepo.RechercherAsync(entrepriseId, terme, ct);
        var dtos = new List<ProduitDto>();
        foreach (var p in liste) dtos.Add(await MapToDtoAsync(p, ct));
        return dtos;
    }

    private async Task<ProduitDto> MapToDtoAsync(Produit p, CancellationToken ct)
    {
        string? categorieNom = null;
        if (p.CategorieId.HasValue)
        {
            var cat = await categorieRepo.ObtenirParIdAsync(p.CategorieId.Value, ct);
            categorieNom = cat?.Nom;
        }
        return new ProduitDto(
            p.Id, p.EntrepriseId, p.CategorieId, categorieNom,
            p.Code, p.Libelle, p.Description, p.PrixUnitaire,
            p.TauxTva, p.Unite, p.Type.ToString(),
            p.EstActif, p.CreeLe, p.ModifieLe);
    }
}
