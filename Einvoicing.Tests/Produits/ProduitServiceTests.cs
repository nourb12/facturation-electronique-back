using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Produits;

public class ProduitServiceTests
{
    private readonly Mock<IProduitRepository> _produitRepo = new();
    private readonly Mock<ICategorieRepository> _categorieRepo = new();

    private ProduitService CreateService() => new(
        _produitRepo.Object,
        _categorieRepo.Object);

    [Fact]
    public async Task CreerAsync_CodeDejaExistant_ThrowConflit()
    {
        
        var entrepriseId = Guid.NewGuid();
        var existing = Produit.Creer(entrepriseId, "P01", "Prod", 10, 19, TypeProduit.Produit);
        _produitRepo.Setup(r => r.ObtenirParCodeAsync(entrepriseId, "P01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var req = new CreerProduitRequest("P01", "Prod", 10, 19, TypeProduit.Produit, null, null, "U");
        var service = CreateService();

        
        Func<Task> act = () => service.CreerAsync(entrepriseId, req);

        
        await act.Should().ThrowAsync<ConflitException>();
    }

    [Fact]
    public async Task CreerAsync_CategorieInexistante_ThrowNotFound()
    {
        
        var entrepriseId = Guid.NewGuid();
        _produitRepo.Setup(r => r.ObtenirParCodeAsync(entrepriseId, "P02", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Produit?)null);
        _categorieRepo.Setup(r => r.ObtenirParIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategorieProduit?)null);

        var req = new CreerProduitRequest("P02", "Prod", 10, 19, TypeProduit.Service, Guid.NewGuid(), null, "H");
        var service = CreateService();

        
        Func<Task> act = () => service.CreerAsync(entrepriseId, req);

        
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreerAsync_ValidInput_PersistAndReturnDto()
    {
        
        var entrepriseId = Guid.NewGuid();
        var categorieId = Guid.NewGuid();
        var categorie = CategorieProduit.Creer(entrepriseId, "CAT", "desc");

        _produitRepo.Setup(r => r.ObtenirParCodeAsync(entrepriseId, "P03", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Produit?)null);
        _categorieRepo.Setup(r => r.ObtenirParIdAsync(categorieId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorie);
        _produitRepo.Setup(r => r.AjouterAsync(It.IsAny<Produit>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _produitRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var req = new CreerProduitRequest("P03", "Prod 3", 120.123m, 7, TypeProduit.Produit, categorieId, "Desc", "U");
        var service = CreateService();

        
        var dto = await service.CreerAsync(entrepriseId, req);

        
        dto.Code.Should().Be("P03");
        dto.Libelle.Should().Be("Prod 3");
        dto.TauxTva.Should().Be(7);
        dto.CategorieId.Should().Be(categorieId);
        dto.CategorieNom.Should().Be("CAT");
        _produitRepo.Verify(r => r.AjouterAsync(It.IsAny<Produit>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MettreAJourAsync_EntrepriseMismatch_ThrowAccesRefuse()
    {
        
        var produit = Produit.Creer(Guid.NewGuid(), "P04", "Prod", 10, 19, TypeProduit.Produit);
        _produitRepo.Setup(r => r.ObtenirParIdAsync(produit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(produit);
        var req = new MettreAJourProduitRequest("New", 12, 7, TypeProduit.Service, null, null, "U");
        var service = CreateService();

        
        Func<Task> act = () => service.MettreAJourAsync(produit.Id, Guid.NewGuid(), req);

        
        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task DesactiverAsync_SetsInactiveAndPersists()
    {
        
        var entrepriseId = Guid.NewGuid();
        var produit = Produit.Creer(entrepriseId, "P05", "Prod", 10, 19, TypeProduit.Produit);
        _produitRepo.Setup(r => r.ObtenirParIdAsync(produit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(produit);
        _produitRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        await service.DesactiverAsync(produit.Id, entrepriseId);

        
        produit.EstActif.Should().BeFalse();
        _produitRepo.Verify(r => r.MettreAJour(produit), Times.Once);
    }
}
