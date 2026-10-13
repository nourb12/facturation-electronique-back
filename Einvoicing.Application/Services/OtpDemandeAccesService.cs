using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class OtpDemandeAccesService(
    IOtpDemandeAccesRepository repository,
    IEmailService emailService,
    ISmsService smsService,
    IRateLimiter rateLimiter) : IOtpDemandeAccesService
{
    public async Task<OtpDemandeAccesEnvoyeDto> EnvoyerAsync(EnvoyerOtpDemandeAccesRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Telephone) || request.Canal is not ("email" or "sms"))
            throw new ValidationMetierException("Choisissez un canal de verification valide.");
        var rateKey = $"access-request-otp:{request.Email.Trim().ToLowerInvariant()}:{request.Telephone.Trim()}";
        if (rateLimiter.EstBloque(rateKey))
            throw new TropDeTentativesException();
        rateLimiter.EnregistrerEchec(rateKey);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = OtpDemandeAcces.Creer(request.Email, request.Telephone, request.Canal, Hash(code));
        await repository.AjouterAsync(otp, ct);
        await repository.SauvegarderAsync(ct);

        var smsEnvoye = true;
        if (request.Canal == "email")
        {
            await emailService.EnvoyerOtpAsync(otp.Email, request.Prenom, code, ct);
        }
        else
        {
            try
            {
                await smsService.EnvoyerAsync(otp.Telephone, $"[TuniFlow] Votre code de verification est {code}. Il expire dans 5 minutes.", ct);
            }
            catch
            {
                smsEnvoye = false;
            }
        }
        return new OtpDemandeAccesEnvoyeDto(otp.Id, otp.ExpireLe, smsEnvoye);
    }

    public async Task<OtpDemandeAccesVerifieDto> VerifierAsync(VerifierOtpDemandeAccesRequest request, CancellationToken ct = default)
    {
        var otp = await repository.ObtenirAsync(request.VerificationId, ct)
            ?? throw new ValidationMetierException("Verification OTP introuvable.");
        if (!otp.EstValide()) throw new ValidationMetierException("Le code a expire ou le nombre d'essais est depasse.");
        if (request.Canal != otp.Canal || !Regex.IsMatch(request.Code ?? string.Empty, "^\\d{6}$"))
            throw new ValidationMetierException("Le code OTP est invalide.");

        var expected = otp.EmailCodeHash;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), SHA256.HashData(Encoding.UTF8.GetBytes(request.Code))))
        {
            otp.EnregistrerEchec();
            repository.MettreAJour(otp);
            await repository.SauvegarderAsync(ct);
            throw new ValidationMetierException("Le code OTP est incorrect.");
        }

        if (request.Canal == "email") otp.VerifierEmail(); else otp.VerifierSms();
        string? token = null;
        if (otp.EmailVerifie || otp.SmsVerifie)
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            otp.DefinirVerificationToken(Hash(token));
        }
        repository.MettreAJour(otp);
        await repository.SauvegarderAsync(ct);
        return new OtpDemandeAccesVerifieDto(otp.EmailVerifie, otp.SmsVerifie, token);
    }

    public async Task VerifierEtConsommerAsync(Guid id, string token, string email, string telephone, CancellationToken ct = default)
    {
        var otp = await repository.ObtenirAsync(id, ct)
            ?? throw new ValidationMetierException("Verification OTP obligatoire avant l'envoi.");
        if (!otp.PeutSoumettre(email, telephone) || !otp.VerifierToken(token))
            throw new ValidationMetierException("La verification email et SMS doit etre terminee avant l'envoi.");
        otp.Utiliser();
        repository.MettreAJour(otp);
        await repository.SauvegarderAsync(ct);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
