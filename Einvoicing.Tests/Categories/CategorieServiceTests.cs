using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Categories;

public class CategorieServiceTests
{
    private readonly Mock<ICategorieRepository> _categorieRepo = new();

    private CategorieService CreateService() => new(_categorieRepo.Object);

    [Fact]
    public async Task CreerAsync_ValidInput_PersistAndReturnDto()
    {
        
        var entrepriseId = Guid.NewGuid();
        var req = new CreerCategorieRequest("Services", "Prestations");
        _categorieRepo.Setup(r => r.AjouterAsync(It.IsAny<CategorieProduit>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _categorieRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        var dto = await service.CreerAsync(entrepriseId, req);

        
        dto.Nom.Should().Be("Services");
        dto.EstActive.Should().BeTrue();
        dto.NbProduits.Should().Be(0);
        _categorieRepo.Verify(r => r.AjouterAsync(It.IsAny<CategorieProduit>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MettreAJourAsync_EntrepriseMismatch_ThrowAccesRefuse()
    {
        
        var categorie = CategorieProduit.Creer(Guid.NewGuid(), "Cat", "Desc");
        _categorieRepo.Setup(r => r.ObtenirParIdAsync(categorie.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorie);
        var req = new MettreAJourCategorieRequest("New", "New desc");
        var service = CreateService();

        
        Func<Task> act = () => service.MettreAJourAsync(categorie.Id, Guid.NewGuid(), req);

        
        await act.Should().ThrowAsync<AccesRefuseException>();
    }

    [Fact]
    public async Task DesactiverAsync_SetsInactiveAndPersists()
    {
        
        var entrepriseId = Guid.NewGuid();
        var categorie = CategorieProduit.Creer(entrepriseId, "Cat", "Desc");
        _categorieRepo.Setup(r => r.ObtenirParIdAsync(categorie.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorie);
        _categorieRepo.Setup(r => r.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        await service.DesactiverAsync(categorie.Id, entrepriseId);

        
        categorie.EstActive.Should().BeFalse();
        _categorieRepo.Verify(r => r.MettreAJour(categorie), Times.Once);
    }
}
