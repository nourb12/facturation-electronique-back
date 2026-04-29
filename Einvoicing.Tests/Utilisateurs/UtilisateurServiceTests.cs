using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Utilisateurs;

public class UtilisateurServiceTests
{
    private readonly Mock<IUtilisateurRepository> _utilisateurRepo = new();
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    private UtilisateurService CreateService() => new(
        _utilisateurRepo.Object, _sessionRepo.Object, _passwordHasher.Object);

    [Fact]
    public async Task CreerAsync_PasswordMismatch_ThrowsValidation()
    {
        var req = new CreerUtilisateurRequest(
            "Prenom", "Nom", "a@b.c", "pass1", "pass2", RoleUtilisateur.Admin);

        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }

    [Fact]
    public async Task CreerAsync_EmailExists_ThrowsConflit()
    {
        var req = new CreerUtilisateurRequest(
            "Prenom", "Nom", "a@b.c", "pass", "pass", RoleUtilisateur.Admin);
        var existing = Utilisateur.Creer("P", "N", "a@b.c", "HASH", RoleUtilisateur.Admin, Guid.NewGuid());
        _utilisateurRepo.Setup(r => r.ObtenirParEmailAsync(req.Email.ToLower(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService();

        Func<Task> act = () => service.CreerAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<ConflitException>();
    }

    [Fact]
    public async Task ChangerMotDePasseAsync_InvalidCurrent_ThrowsValidation()
    {
        var user = Utilisateur.Creer("P", "N", "a@b.c", "HASH", RoleUtilisateur.Admin, Guid.NewGuid());
        _utilisateurRepo.Setup(r => r.ObtenirParIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(p => p.Verifier("bad", user.MotDePasseHash)).Returns(false);

        var req = new ChangerMotDePasseRequest("bad", "newpass", "newpass");
        var service = CreateService();

        Func<Task> act = () => service.ChangerMotDePasseAsync(user.Id, req);

        await act.Should().ThrowAsync<ValidationMetierException>();
    }
}