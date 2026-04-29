




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class PaiementService(
    IPaiementRepository paiementRepo,
    IFactureRepository factureRepo
) : IPaiementService
{
    public async Task<PaiementDto> EnregistrerAsync(
        Guid entrepriseId, Guid enregistrePar,
        EnregistrerPaiementRequest req, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(req.FactureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (facture.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        if (facture.Statut != StatutFacture.Acceptee
            && facture.Statut != StatutFacture.PartiellemementPayee)
            throw new ValidationMetierException(
                "La facture doit être acceptée pour enregistrer un paiement.");

        if (req.Montant > facture.MontantRestant)
            throw new ValidationMetierException(
                $"Le montant ({req.Montant:N3}) dépasse le solde restant ({facture.MontantRestant:N3}).");

        var paiement = Paiement.Creer(
            req.FactureId, entrepriseId, enregistrePar,
            req.Montant, req.Mode, req.DatePaiement,
            req.Reference, req.Banque, req.Notes);

        await paiementRepo.AjouterAsync(paiement, ct);

        facture.EnregistrerPaiement(req.Montant, enregistrePar);
        factureRepo.MettreAJour(facture);
        await paiementRepo.SauvegarderAsync(ct);

        return new PaiementDto(
            paiement.Id, paiement.FactureId, facture.Numero,
            paiement.Montant, paiement.Devise,
            paiement.Mode.ToString(), paiement.Reference,
            paiement.Banque, paiement.Notes,
            paiement.DatePaiement, paiement.CreeLe);
    }

    public async Task<ListePaiementsDto> ListerParFactureAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirParIdAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");

        if (facture.EntrepriseId != entrepriseId)
            throw new AccesRefuseException();

        var paiements = await paiementRepo.ListerParFactureAsync(factureId, ct);
        var totalPaye = paiements.Sum(p => p.Montant);

        var dtos = paiements.Select(p => new PaiementDto(
            p.Id, p.FactureId, facture.Numero,
            p.Montant, p.Devise, p.Mode.ToString(),
            p.Reference, p.Banque, p.Notes,
            p.DatePaiement, p.CreeLe)).ToList();

        return new ListePaiementsDto(dtos, dtos.Count, totalPaye, facture.MontantRestant);
    }

    public async Task<ListePaiementsDto> ListerParEntrepriseAsync(
        Guid entrepriseId, int page, int parPage, CancellationToken ct = default)
    {
        var (items, total) = await paiementRepo.ListerParEntrepriseAsync(
            entrepriseId, page, parPage, ct);

        var dtos = new List<PaiementDto>();
        foreach (var p in items)
        {
            var facture = await factureRepo.ObtenirParIdAsync(p.FactureId, ct);
            dtos.Add(new PaiementDto(
                p.Id, p.FactureId, facture?.Numero ?? "-",
                p.Montant, p.Devise, p.Mode.ToString(),
                p.Reference, p.Banque, p.Notes,
                p.DatePaiement, p.CreeLe));
        }

        var totalPaye = dtos.Sum(d => d.Montant);
        return new ListePaiementsDto(dtos, total, totalPaye, 0);
    }
}