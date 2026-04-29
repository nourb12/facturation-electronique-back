using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.ParametresFiscaux;

public class ParametreFiscalServiceTests
{
    private readonly Mock<IParametreFiscalRepository> _repo = new();

    private ParametreFiscalService CreateService() => new(_repo.Object);

    [Fact]
    public async Task CreerAsync_InvalidType_ThrowsValidation()
    {
        var req = new CreerParametreFiscalRequest(
            "Timbre", 1m, "BadType", "Positif",
            "AvantTva", "Manuel", false, new List<string>());
        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }

    [Fact]
    public async Task MettreAJourAsync_EntrepriseMismatch_ThrowAccesRefuse()
    {
        var entrepriseId = Guid.NewGuid();
        var param = ParametreFiscal.Creer(
            entrepriseId, "Timbre", 1m,
            TypeParametreFiscal.Pourcentage, SigneParametreFiscal.Positif,
            OrdreCalcul.AvantTva, UtilisationParametreFiscal.Manuel, false,
            new List<string>());

        _repo.Setup(r => r.ObtenirParIdAsync(param.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(param);

        var req = new MettreAJourParametreFiscalRequest(
            "Timbre", 1m, "Pourcentage", "Positif",
            "AvantTva", "Manuel", false, new List<string>());
        var service = CreateService();

        Func<Task> act = () => service.MettreAJourAsync(param.Id, Guid.NewGuid(), req);

        await act.Should().ThrowAsync<AccesRefuseException>();
    }
}