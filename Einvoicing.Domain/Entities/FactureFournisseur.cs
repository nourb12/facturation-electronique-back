using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class FactureFournisseur
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid FournisseurId { get; private set; }
    public Guid CreePar { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string Statut { get; private set; } = "Brouillon";
    public DateTime DateFacture { get; private set; }
    public DateTime DateEcheance { get; private set; }
    public decimal TotalHt { get; private set; }
    public decimal TotalTva { get; private set; }
    public decimal TotalTtc { get; private set; }
    public decimal TauxRetenue { get; private set; }
    public decimal MontantRetenue { get; private set; }
    public decimal NetAPayer { get; private set; }
    public decimal MontantPaye { get; private set; }
    public string LignesJson { get; private set; } = "[]";
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public Guid? TransactionOrigineId { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private FactureFournisseur() { }

    public static FactureFournisseur Creer(Guid entrepriseId, Guid fournisseurId, Guid creePar, string numero,
        DateTime dateFacture, DateTime dateEcheance, decimal totalHt, decimal totalTva, decimal tauxRetenue,
        string lignesJson, string? reference, string? notes, Guid? transactionOrigineId = null)
    {
        if (string.IsNullOrWhiteSpace(numero)) throw new ValidationMetierException("Le numéro fournisseur est requis.");
        if (totalHt < 0 || totalTva < 0) throw new ValidationMetierException("Les montants de facture sont invalides.");
        if (dateEcheance.Date < dateFacture.Date) throw new ValidationMetierException("L'échéance doit être postérieure à la facture.");
        var totalTtc = Math.Round(totalHt + totalTva, 3);
        if (totalTtc <= 0) throw new ValidationMetierException("Le total TTC doit être positif.");
        // Retenue a la source calculee sur le montant TTC (art. 52 du Code de l'IRPP et de l'IS).
        var retenue = Math.Round(totalTtc * Math.Max(0, tauxRetenue) / 100m, 3, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;
        return new FactureFournisseur
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, FournisseurId = fournisseurId, CreePar = creePar,
            Numero = numero.Trim(), DateFacture = dateFacture, DateEcheance = dateEcheance,
            TotalHt = Math.Round(totalHt, 3), TotalTva = Math.Round(totalTva, 3), TotalTtc = totalTtc,
            TauxRetenue = Math.Round(Math.Max(0, tauxRetenue), 3), MontantRetenue = retenue,
            NetAPayer = Math.Round(totalTtc - retenue, 3), LignesJson = lignesJson,
            Reference = reference?.Trim(), Notes = notes?.Trim(), TransactionOrigineId = transactionOrigineId,
            CreeLe = now, ModifieLe = now
        };
    }

    public void Valider() { if (Statut != "Brouillon") throw new ValidationMetierException("Seul un brouillon peut être validé."); Statut = "Validée"; ModifieLe = DateTime.UtcNow; }
    public void MettreAJour(DateTime dateFacture, DateTime dateEcheance, decimal totalHt, decimal totalTva, decimal tauxRetenue, string lignesJson, string? reference, string? notes)
    {
        if (Statut != "Brouillon") throw new ValidationMetierException("Seule une facture fournisseur brouillon peut être modifiée.");
        if (dateEcheance.Date < dateFacture.Date || totalHt < 0 || totalTva < 0) throw new ValidationMetierException("Les données de facture sont invalides.");
        var totalTtc = Math.Round(totalHt + totalTva, 3); if (totalTtc <= 0) throw new ValidationMetierException("Le total TTC doit être positif.");
        DateFacture = dateFacture; DateEcheance = dateEcheance; TotalHt = Math.Round(totalHt, 3); TotalTva = Math.Round(totalTva, 3); TotalTtc = totalTtc;
        TauxRetenue = Math.Round(Math.Max(0, tauxRetenue), 3); MontantRetenue = Math.Round(TotalTtc * TauxRetenue / 100m, 3, MidpointRounding.AwayFromZero); NetAPayer = Math.Round(TotalTtc - MontantRetenue, 3);
        LignesJson = lignesJson; Reference = reference?.Trim(); Notes = notes?.Trim(); ModifieLe = DateTime.UtcNow;
    }
    public void EnregistrerPaiement(decimal montant)
    {
        if (Statut is not ("Validée" or "Partiellement payée")) throw new ValidationMetierException("La facture fournisseur doit être validée avant paiement.");
        if (montant <= 0 || montant > MontantRestant) throw new ValidationMetierException("Le montant de paiement est invalide.");
        MontantPaye = Math.Round(MontantPaye + montant, 3);
        Statut = MontantPaye >= NetAPayer ? "Payée" : "Partiellement payée";
        ModifieLe = DateTime.UtcNow;
    }
    public decimal MontantRestant => Math.Round(NetAPayer - MontantPaye, 3);
}
