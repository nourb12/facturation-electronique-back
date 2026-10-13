using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class PaiementFournisseur
{
    public Guid Id { get; private set; }
    public Guid FactureFournisseurId { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid EnregistrePar { get; private set; }
    public decimal Montant { get; private set; }
    public ModePaiement Mode { get; private set; }
    public DateTime DatePaiement { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreeLe { get; private set; }
    private PaiementFournisseur() { }
    public static PaiementFournisseur Creer(Guid factureId, Guid entrepriseId, Guid userId, decimal montant, ModePaiement mode, DateTime date, string? reference, string? notes)
    {
        if (montant <= 0) throw new ValidationMetierException("Le montant du paiement doit être positif.");
        return new PaiementFournisseur { Id = Guid.NewGuid(), FactureFournisseurId = factureId, EntrepriseId = entrepriseId, EnregistrePar = userId, Montant = Math.Round(montant, 3), Mode = mode, DatePaiement = date, Reference = reference?.Trim(), Notes = notes?.Trim(), CreeLe = DateTime.UtcNow };
    }
}
