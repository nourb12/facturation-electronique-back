using System.Threading;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using FluentAssertions;
using Moq;

namespace Einvoicing.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUtilisateurRepository> _utilisateurRepo = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo = new();
    private readonly Mock<IOtpRepository> _otpRepo = new();
    private readonly Mock<IEntrepriseRepository> _entrepriseRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITotpService> _totpService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IRateLimiter> _rateLimiter = new();

    private AuthService CreateService() => new(
        _utilisateurRepo.Object,
        _refreshTokenRepo.Object,
        _otpRepo.Object,
        _entrepriseRepo.Object,
        _jwtService.Object,
        _passwordHasher.Object,
        _totpService.Object,
        _emailService.Object,
        _rateLimiter.Object);

    [Fact]
    public async Task ConnecterAsync_ValidCredentials_ReturnsAuthTokens()
    {
        
        var user = Utilisateur.Creer("John", "Doe", "john@test.com", "hash", RoleUtilisateur.Admin);
        _rateLimiter.Setup(x => x.EstBloque(It.IsAny<string>())).Returns(false);
        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verifier("pwd123", user.MotDePasseHash)).Returns(true);
        _jwtService.Setup(x => x.GenererAccessToken(user)).Returns("access-token");
        _jwtService.Setup(x => x.GenererRefreshToken()).Returns("refresh-token");
        _jwtService.Setup(x => x.ExtraireClaimsTokenExpire("access-token"))
            .Returns(new ClaimsResult(user.Id, "jwt-id", user.Role.ToString()));
        _refreshTokenRepo.Setup(x => x.AjouterAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _utilisateurRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        var result = await service.ConnecterAsync(new LoginRequest(user.Email, "pwd123"), null, null);

        
        var auth = result.Should().BeOfType<AuthResponse>().Subject;
        auth.AccessToken.Should().Be("access-token");
        auth.RefreshToken.Should().Be("refresh-token");
        auth.Utilisateur.Email.Should().Be(user.Email);
        _refreshTokenRepo.Verify(x => x.AjouterAsync(
            It.Is<RefreshToken>(rt =>
                rt.Token == "refresh-token" &&
                rt.JwtId == "jwt-id" &&
                rt.UtilisateurId == user.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnecterAsync_InvalidPassword_ThrowsIdentifiantsInvalides()
    {
        
        var user = Utilisateur.Creer("Jane", "Doe", "jane@test.com", "hash", RoleUtilisateur.Admin);
        _rateLimiter.Setup(x => x.EstBloque(It.IsAny<string>())).Returns(false);
        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(x => x.Verifier("wrong", user.MotDePasseHash)).Returns(false);

        var service = CreateService();

        
        Func<Task> act = () => service.ConnecterAsync(new LoginRequest(user.Email, "wrong"), null, null);

        
        await act.Should().ThrowAsync<IdentifiantsInvalidesException>();
        _rateLimiter.Verify(x => x.EnregistrerEchec(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InscrireAsync_PasswordHashedAndUserPersisted()
    {
        
                var request = new RegisterRequest(
            "Alan", "Turing", "alan@test.com", "Sup3rPwd!", "Sup3rPwd!",
            "ACME", "1234567ABM000",
            "1 Rue Habib Bourguiba", "Tunis", "1000",
            "+21620000000", "https://acme.tn", "TND");

        Utilisateur? savedUser = null;
        _utilisateurRepo.Setup(x => x.ObtenirParEmailAsync(request.Email.ToLower(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Utilisateur?)null);
        _entrepriseRepo.Setup(x => x.ObtenirParMatriculeAsync(request.MatriculeFiscal, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entreprise?)null);
        _passwordHasher.Setup(x => x.Hacher(request.MotDePasse)).Returns("hashed-pwd");
        _utilisateurRepo.Setup(x => x.AjouterAsync(It.IsAny<Utilisateur>(), It.IsAny<CancellationToken>()))
            .Callback<Utilisateur, CancellationToken>((u, _) => savedUser = u)
            .Returns(Task.CompletedTask);
        _utilisateurRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _entrepriseRepo.Setup(x => x.AjouterAsync(It.IsAny<Entreprise>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _entrepriseRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _emailService.Setup(x => x.EnvoyerBienvenueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _jwtService.Setup(x => x.GenererAccessToken(It.IsAny<Utilisateur>())).Returns("access-token");
        _jwtService.Setup(x => x.GenererRefreshToken()).Returns("refresh-token");
        _jwtService.Setup(x => x.ExtraireClaimsTokenExpire("access-token"))
            .Returns(() => new ClaimsResult(savedUser?.Id ?? Guid.NewGuid(), "jwt-id", RoleUtilisateur.Admin.ToString()));
        _refreshTokenRepo.Setup(x => x.AjouterAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokenRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        var response = await service.InscrireAsync(request);

        
        savedUser.Should().NotBeNull();
        savedUser!.MotDePasseHash.Should().Be("hashed-pwd");
        response.AccessToken.Should().Be("access-token");
        response.Utilisateur.Email.Should().Be(request.Email.ToLower());
    }

    [Fact]
    public async Task RafraichirTokenAsync_ValidRefreshToken_RotatesTokens()
    {
        
        var user = Utilisateur.Creer("Sarah", "Connor", "sarah@test.com", "hash", RoleUtilisateur.Admin);
        var existingRefresh = RefreshToken.Creer(user.Id, "valid-refresh", "jwt-old", "127.0.0.1", "agent");

        _jwtService.Setup(x => x.ExtraireClaimsTokenExpire("expired-access"))
            .Returns(new ClaimsResult(user.Id, "jwt-old", user.Role.ToString()));
        _refreshTokenRepo.Setup(x => x.ObtenirParTokenAsync("valid-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefresh);
        _refreshTokenRepo.Setup(x => x.SauvegarderAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _utilisateurRepo.Setup(x => x.ObtenirParIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtService.Setup(x => x.GenererAccessToken(user)).Returns("new-access");
        _jwtService.Setup(x => x.GenererRefreshToken()).Returns("new-refresh");
        _jwtService.Setup(x => x.ExtraireClaimsTokenExpire("new-access"))
            .Returns(new ClaimsResult(user.Id, "jwt-new", user.Role.ToString()));
        _refreshTokenRepo.Setup(x => x.AjouterAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        
        var response = await service.RafraichirTokenAsync(
            new RefreshTokenRequest("expired-access", "valid-refresh"),
            "127.0.0.1",
            "agent");

        
        response.AccessToken.Should().Be("new-access");
        response.RefreshToken.Should().Be("new-refresh");
        existingRefresh.EstValide().Should().BeFalse();
        _refreshTokenRepo.Verify(x => x.AjouterAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

