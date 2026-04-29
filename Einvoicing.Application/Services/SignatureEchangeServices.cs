using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class SignatureService(
    ISignatureRepository signatureRepo,
    IFactureRepository factureRepo,
    ISignatureProvider signatureProvider
) : ISignatureService
{
    public async Task<SignatureDto> DemanderSignatureAsync(
        Guid factureId, Guid entrepriseId, Guid demandeePar, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        if (facture.Statut != StatutFacture.Conforme)
            throw new ValidationMetierException(
                "Seule une facture conforme TEIF peut être signée.");

        if (string.IsNullOrWhiteSpace(facture.HashIntegrite))
            throw new ValidationMetierException(
                "Le XML TEIF doit être généré avant la signature.");

        var existing = await signatureRepo.ObtenirParFactureAsync(factureId, ct);
        if (existing?.EstSignee == true)
            throw new ValidationMetierException("Cette facture est déjà signée.");

        var signatureReq = SignatureRequest.Creer(factureId, entrepriseId, demandeePar);
        await signatureRepo.AjouterAsync(signatureReq, ct);
        await signatureRepo.SauvegarderAsync(ct);

        try
        {
            var (valeur, certificat) = await signatureProvider.SignerAsync(
                facture.HashIntegrite!, ct);

            signatureReq.Signer(valeur, certificat);
            signatureRepo.MettreAJour(signatureReq);
            await signatureRepo.SauvegarderAsync(ct);
        }
        catch (Exception ex)
        {
            signatureReq.Echouer(ex.Message);
            signatureRepo.MettreAJour(signatureReq);
            await signatureRepo.SauvegarderAsync(ct);
        }

        return MapToDto(signatureReq);
    }

    public async Task<SignatureDto> ObtenirStatutAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var sig = await signatureRepo.ObtenirParFactureAsync(factureId, ct)
            ?? throw new NotFoundException("Aucune demande de signature trouvée.");
        return MapToDto(sig);
    }

    public async Task<SignatureDto> RelancerAsync(
        Guid signatureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var sig = await signatureRepo.ObtenirParIdAsync(signatureId, ct)
            ?? throw new NotFoundException("Demande de signature introuvable.");

        if (sig.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        sig.Relancer();
        signatureRepo.MettreAJour(sig);

        var facture = await factureRepo.ObtenirAvecDetailsAsync(sig.FactureId, ct)!;

        try
        {
            var (valeur, certificat) = await signatureProvider.SignerAsync(
                facture!.HashIntegrite!, ct);
            sig.Signer(valeur, certificat);
        }
        catch (Exception ex)
        {
            sig.Echouer(ex.Message);
        }

        signatureRepo.MettreAJour(sig);
        await signatureRepo.SauvegarderAsync(ct);
        return MapToDto(sig);
    }

    private static SignatureDto MapToDto(SignatureRequest s) => new(
        s.Id, s.FactureId, s.Statut.ToString(),
        s.SignatureValue, s.CertificatId,
        s.MessageErreur, s.NbTentatives,
        s.CreeLe, s.SigneeA);
}





public interface ISignatureProvider
{
    Task<(string SignatureValue, string CertificatId)> SignerAsync(
        string hashIntegrite, CancellationToken ct = default);
}



public sealed class MockSignatureProvider : ISignatureProvider
{
    public Task<(string SignatureValue, string CertificatId)> SignerAsync(
        string hashIntegrite, CancellationToken ct = default)
    {
        var sig = $"SIG-MOCK-{Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(hashIntegrite))
        ).Substring(0, 32)}";

        return Task.FromResult((sig, "CERT-MOCK-2024-TF"));
    }
}





public sealed class EchangeTtnService(
    IEchangeRepository echangeRepo,
    IFactureRepository factureRepo,
    ISignatureRepository signatureRepo
) : IEchangeTtnService
{
    public async Task<EchangeDto> EnvoyerAsync(
        Guid factureId, Guid entrepriseId, Guid envoyePar, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        if (facture.Statut != StatutFacture.Conforme)
            throw new ValidationMetierException(
                "La facture doit être conforme TEIF avant transmission TTN.");

        var sig = await signatureRepo.ObtenirParFactureAsync(factureId, ct);
        if (sig?.EstSignee != true)
            throw new ValidationMetierException(
                "La facture doit être signée avant transmission TTN.");

        var echange = ExternalExchange.Creer(factureId, entrepriseId, envoyePar);
        await echangeRepo.AjouterAsync(echange, ct);

        echange.MarquerEnvoye();
        facture.MarquerTransmise(envoyePar);

        echangeRepo.MettreAJour(echange);
        factureRepo.MettreAJour(facture);
        await echangeRepo.SauvegarderAsync(ct);

        return MapToDto(echange);
    }

    public async Task<EchangeDto> SimulerReponseAsync(
        Guid echangeId, string scenario, CancellationToken ct = default)
    {
        var echange = await echangeRepo.ObtenirParIdAsync(echangeId, ct)
            ?? throw new NotFoundException("Échange introuvable.");

        var facture = await factureRepo.ObtenirAvecDetailsAsync(echange.FactureId, ct)!;
        var userId = echange.EnvoyePar;

        switch (scenario.ToLowerInvariant())
        {
            case "accepte":
                echange.MarquerAcknowledge();
                echange.MarquerAccepte();
                facture!.MarquerAcceptee(userId);
                break;

            case "rejete":
                echange.MarquerRejete(
                    "ERR-TTN-4001",
                    "Facture rejetée par le système TTN.",
                    "Matricule fiscal fournisseur invalide");
                facture!.MarquerRejetee(userId, "Rejet TTN — Matricule fiscal invalide");
                break;

            case "erreur":
                echange.MarquerErreur("Timeout de connexion TTN.");
                break;

            default:
                throw new ValidationMetierException(
                    "Scénario inconnu. Valeurs : accepte, rejete, erreur.");
        }

        echangeRepo.MettreAJour(echange);
        factureRepo.MettreAJour(facture!);
        await echangeRepo.SauvegarderAsync(ct);

        return MapToDto(echange);
    }

    public async Task<EchangeDto> ObtenirStatutAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var echange = await echangeRepo.ObtenirDernierParFactureAsync(factureId, ct)
            ?? throw new NotFoundException("Aucun échange TTN trouvé.");
        return MapToDto(echange);
    }

    public async Task<EchangeDto> RelancerAsync(
        Guid echangeId, Guid entrepriseId, CancellationToken ct = default)
    {
        var echange = await echangeRepo.ObtenirParIdAsync(echangeId, ct)
            ?? throw new NotFoundException("Échange introuvable.");

        if (echange.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        if (!echange.PeutEtreRelance)
            throw new ValidationMetierException(
                "Cet échange ne peut pas être relancé (échec définitif ou déjà accepté).");

        echange.MarquerEnvoye();
        echangeRepo.MettreAJour(echange);
        await echangeRepo.SauvegarderAsync(ct);

        return MapToDto(echange);
    }

    private static EchangeDto MapToDto(ExternalExchange e) => new(
        e.Id, e.FactureId, e.CorrelationId,
        e.Statut.ToString(), e.ReponseCode, e.ReponseMessage,
        e.MotifRejet, e.RetryCount, e.CreeLe, e.AccepteeA, e.RejeteeLe);
}
