using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Paiement
{
    public Guid Id { get; private set; }
    public Guid FactureId { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid EnregistrePar { get; private set; }

    public decimal Montant { get; private set; }
    public string Devise { get; private set; } = "TND";
    public ModePaiement Mode { get; private set; }
    public string? Reference { get; private set; }
    public string? Banque { get; private set; }
    public string? Notes { get; private set; }
    public DateTime DatePaiement { get; private set; }
    public DateTime CreeLe { get; private set; }

    private Paiement() { }

    public static Paiement Creer(
        Guid factureId, Guid entrepriseId, Guid enregistrePar,
        decimal montant, ModePaiement mode,
        DateTime datePaiement,
        string? reference = null,
        string? banque = null,
        string? notes = null,
        string devise = "TND")
    {
        if (montant <= 0)
            throw new ValidationMetierException("Le montant du paiement doit être positif.");

        return new Paiement
        {
            Id = Guid.NewGuid(),
            FactureId = factureId,
            EntrepriseId = entrepriseId,
            EnregistrePar = enregistrePar,
            Montant = Math.Round(montant, 3),
            Devise = devise,
            Mode = mode,
            Reference = reference?.Trim(),
            Banque = banque?.Trim(),
            Notes = notes?.Trim(),
            DatePaiement = datePaiement,
            CreeLe = DateTime.UtcNow
        };
    }
}
