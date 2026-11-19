using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class DocumentCommercial
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid CreePar { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Numero { get; private set; } = string.Empty;
    public string Statut { get; private set; } = "Brouillon";
    public DateTime DateDocument { get; private set; }
    public DateTime? DateEcheance { get; private set; }
    public decimal TotalHt { get; private set; }
    public decimal TotalTva { get; private set; }
    public decimal TotalTtc { get; private set; }
    public string LignesJson { get; private set; } = "[]";
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public Guid? DocumentOrigineId { get; private set; }
    public Guid? FactureCreeeId { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private static readonly Dictionary<string, string[]> StatutsAutorises = new()
    {
        ["bon_commande"] = ["Brouillon", "Reçu", "Confirmé", "En cours", "Livré", "Annulé", "Facturé"],
        ["bon_livraison"] = ["Brouillon", "Préparé", "Expédié", "Livré", "Litige", "Facturé"],
    };

    private DocumentCommercial() { }

    public static DocumentCommercial Creer(Guid entrepriseId, Guid clientId, Guid creePar, string type, string numero,
        DateTime dateDocument, DateTime? dateEcheance, decimal totalHt, decimal totalTva, decimal totalTtc,
        string lignesJson, string? reference, string? notes, Guid? documentOrigineId = null)
    {
        if (type is not ("bon_commande" or "bon_livraison"))
            throw new ValidationMetierException("Type de document commercial invalide.");
        if (string.IsNullOrWhiteSpace(numero)) throw new ValidationMetierException("Le numero est requis.");
        if (totalTtc <= 0) throw new ValidationMetierException("Le total TTC doit etre positif.");
        var now = DateTime.UtcNow;
        return new DocumentCommercial
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, ClientId = clientId, CreePar = creePar,
            Type = type, Numero = numero.Trim(), DateDocument = dateDocument, DateEcheance = dateEcheance,
            TotalHt = Math.Round(totalHt, 3), TotalTva = Math.Round(totalTva, 3), TotalTtc = Math.Round(totalTtc, 3),
            LignesJson = lignesJson, Reference = reference?.Trim(), Notes = notes?.Trim(), DocumentOrigineId = documentOrigineId,
            CreeLe = now, ModifieLe = now
        };
    }

    public void MettreAJour(string statut, DateTime? dateEcheance, string lignesJson, decimal totalHt, decimal totalTva, decimal totalTtc, string? reference, string? notes)
    {
        if (FactureCreeeId.HasValue) throw new ValidationMetierException("Un document deja facture ne peut plus etre modifie.");
        if (totalTtc <= 0) throw new ValidationMetierException("Le total TTC doit etre positif.");
        var nouveauStatut = string.IsNullOrWhiteSpace(statut) ? Statut : statut.Trim();
        if (StatutsAutorises.TryGetValue(Type, out var autorises) && !autorises.Contains(nouveauStatut))
            throw new ValidationMetierException($"Statut « {nouveauStatut} » invalide pour ce document.");
        if (Statut == "Annulé" && nouveauStatut != "Annulé")
            throw new ValidationMetierException("Un document annulé ne peut plus changer de statut.");
        Statut = nouveauStatut; DateEcheance = dateEcheance;
        LignesJson = lignesJson; TotalHt = Math.Round(totalHt, 3); TotalTva = Math.Round(totalTva, 3); TotalTtc = Math.Round(totalTtc, 3);
        Reference = reference?.Trim(); Notes = notes?.Trim(); ModifieLe = DateTime.UtcNow;
    }

    public void MarquerConverti(Guid factureId) { FactureCreeeId = factureId; Statut = "Facturé"; ModifieLe = DateTime.UtcNow; }
}
