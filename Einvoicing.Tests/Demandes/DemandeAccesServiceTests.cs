using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Demandes;

public class DemandeAccesServiceTests
{
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();
    private readonly Mock<IUtilisateurRepository> _utilisateurRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<IOcrClient> _ocrClient = new();
    private readonly KycScoringService _kycScoring = new();

    private DemandeAccesService CreateService() => new(
        _entrepriseRepo.Object,
        _utilisateurRepo.Object,
        _emailService.Object,
        _passwordHasher.Object,
        _fileStorage.Object,
        _kycScoring,
        _ocrClient.Object);

    [Fact]
    public async Task SoumettreDemandeAsync_NormaliseMatriculeEtCreeEntreprise()
    {
        var req = BuildRequest("1234567A/B/M/000");

        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(req.Email.ToLower(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Utilisateur?)null);
        _entrepriseRepo.Setup(x => x.ObtenirParMatriculeAsync("1234567ABM000", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entreprise?)null);

        _ocrClient.Setup(x => x.ExtractAsync(It.IsAny<IEnumerable<Microsoft.AspNetCore.Http.IFormFile?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OcrExtractionResult?)null);

        Entreprise? savedEntreprise = null;
        _entrepriseRepo.Setup(x => x.AjouterAsync(It.IsAny<Entreprise>(), It.IsAny<CancellationToken>()))
            .Callback<Entreprise, CancellationToken>((e, _) => savedEntreprise = e)
            .Returns(Task.CompletedTask);
        _entrepriseRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Utilisateur? savedUser = null;
        _utilisateurRepo.Setup(x => x.AjouterAsync(It.IsAny<Utilisateur>(), It.IsAny<CancellationToken>()))
            .Callback<Utilisateur, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);
        _utilisateurRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _passwordHasher.Setup(x => x.Hacher(It.IsAny<string>())).Returns("HASH");
        _emailService.Setup(x => x.EnvoyerConfirmationDemandeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        var result = await service.SoumettreDemandeAsync(req);

        savedEntreprise.Should().NotBeNull();
        savedEntreprise!.MatriculeFiscal.Should().Be("1234567ABM000");
        savedEntreprise.EstActive.Should().BeFalse();
        savedUser.Should().NotBeNull();
        savedUser!.Statut.Should().Be(StatutCompte.EnAttente);
        savedUser.Role.Should().Be(RoleUtilisateur.ResponsableEntreprise);
        result.Reference.Should().StartWith("MZN-1234567-");
    }

    [Fact]
    public async Task SoumettreDemandeAsync_AssigneRoleFinancierDepuisLePoste()
    {
        var req = BuildRequest("1234567A/B/M/001");
        req.RespFonction = "Responsable Administratif et Financier";

        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(req.Email.ToLower(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Utilisateur?)null);
        _entrepriseRepo.Setup(x => x.ObtenirParMatriculeAsync("1234567ABM001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entreprise?)null);
        _ocrClient.Setup(x => x.ExtractAsync(It.IsAny<IEnumerable<Microsoft.AspNetCore.Http.IFormFile?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OcrExtractionResult?)null);
        _entrepriseRepo.Setup(x => x.AjouterAsync(It.IsAny<Entreprise>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _entrepriseRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Utilisateur? savedUser = null;
        _utilisateurRepo.Setup(x => x.AjouterAsync(It.IsAny<Utilisateur>(), It.IsAny<CancellationToken>()))
            .Callback<Utilisateur, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);
        _utilisateurRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _passwordHasher.Setup(x => x.Hacher(It.IsAny<string>())).Returns("HASH");
        _emailService.Setup(x => x.EnvoyerConfirmationDemandeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        await service.SoumettreDemandeAsync(req);

        savedUser.Should().NotBeNull();
        savedUser!.Role.Should().Be(RoleUtilisateur.ResponsableFinancier);
        savedUser.Departement.Should().Be("Finance");
    }

    [Fact]
    public async Task SoumettreDemandeAsync_MatriculeInvalide_ThrowValidation()
    {
        var req = BuildRequest("123");
        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(req.Email.ToLower(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Utilisateur?)null);

        var service = CreateService();

        Func<Task> act = () => service.SoumettreDemandeAsync(req);

        await act.Should().ThrowAsync<ValidationMetierException>();
        _entrepriseRepo.Verify(x => x.AjouterAsync(It.IsAny<Entreprise>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SoumettreDemandeAccesRequest BuildRequest(string matricule) => new()
    {
        RaisonSociale = "ACME",
        MatriculeFiscal = matricule,
        Email = "contact@acme.tn",
        Telephone = "71234567",
        FormeJuridique = "SARL",
        NomEntreprise = "ACME",
        Adresse = "1 rue",
        Gouvernorat = "Tunis",
        CodePostal = "1000",
        SiteWeb = "https://acme.tn",
        DevisePrincipale = "TND",
        TelEntreprise = "71234567",
        RespPrenom = "Sarra",
        RespNom = "Trabelsi",
        RespFonction = "Gerante",
        RespTel = "71234567"
    };
}
