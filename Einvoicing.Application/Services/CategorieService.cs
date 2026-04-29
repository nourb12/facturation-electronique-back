




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class CategorieService(ICategorieRepository categorieRepo) : ICategorieService
{
    public async Task<CategorieDto> CreerAsync(
        Guid entrepriseId, CreerCategorieRequest req, CancellationToken ct = default)
    {
        var categorie = CategorieProduit.Creer(entrepriseId, req.Nom, req.Description);
        await categorieRepo.AjouterAsync(categorie, ct);
        await categorieRepo.SauvegarderAsync(ct);
        return MapToDto(categorie);
    }

    public async Task<IReadOnlyList<CategorieDto>> ListerAsync(
        Guid entrepriseId, CancellationToken ct = default)
    {
        var liste = await categorieRepo.ListerAsync(entrepriseId, ct);
        return liste.Select(MapToDto).ToList();
    }

    public async Task<CategorieDto> MettreAJourAsync(
        Guid id, Guid entrepriseId, MettreAJourCategorieRequest req, CancellationToken ct = default)
    {
        var categorie = await categorieRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Catégorie introuvable.");
        if (categorie.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        categorie.MettreAJour(req.Nom, req.Description);
        categorieRepo.MettreAJour(categorie);
        await categorieRepo.SauvegarderAsync(ct);
        return MapToDto(categorie);
    }

    public async Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default)
    {
        var categorie = await categorieRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Catégorie introuvable.");
        if (categorie.EntrepriseId != entrepriseId) throw new AccesRefuseException();
        categorie.Desactiver();
        categorieRepo.MettreAJour(categorie);
        await categorieRepo.SauvegarderAsync(ct);
    }

    private static CategorieDto MapToDto(CategorieProduit c) => new(
        c.Id, c.EntrepriseId, c.Nom, c.Description,
        c.EstActive, c.Produits.Count, c.CreeLe);
}
