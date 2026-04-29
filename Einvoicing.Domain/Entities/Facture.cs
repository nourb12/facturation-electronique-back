using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Facture
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid CreePar { get; private set; }

    public string Numero { get; private set; } = string.Empty;
    public string? Reference { get; private set; }

    public StatutFacture Statut { get; private set; }
    public TypeFacture TypeFacture { get; private set; }
    public ModePaiement ModePaiement { get; private set; }

    public string Devise { get; private set; } = "TND";
    public DateTime DateEmission { get; private set; }
    public DateTime DateEcheance { get; private set; }
    public DateTime? DatePaiement { get; private set; }

    public decimal TotalHt { get; private set; }
    public decimal TotalTva { get; private set; }
    public decimal TotalTtc { get; private set; }
    public decimal MontantPaye { get; private set; }

    public string? Notes { get; private set; }
    public string? ConditionsPaiement { get; private set; }

    public string? XmlTeif { get; private set; }
    public string? VersionTeif { get; private set; }
    public string? HashIntegrite { get; private set; }

    public Guid? FactureOrigineId { get; private set; }

    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private readonly List<LigneFacture> _lignes = [];
    private readonly List<HistoriqueFacture> _historique = [];

    public IReadOnlyCollection<LigneFacture> Lignes => _lignes.AsReadOnly();
    public IReadOnlyCollection<HistoriqueFacture> Historique => _historique.AsReadOnly();

    private Facture() { }

    public static Facture Creer(
        Guid entrepriseId,
        Guid clientId,
        Guid creePar,
        string numero,
        TypeFacture typeFacture,
        ModePaiement modePaiement,
        DateTime dateEcheance,
        string? reference = null,
        string? notes = null,
        string? conditionsPaiement = null,
        string devise = "TND",
        Guid? factureOrigineId = null)
    {
        if (dateEcheance.Date < DateTime.UtcNow.Date)
            throw new ValidationMetierException("La date d'échéance ne peut pas être dans le passé.");

        var facture = new Facture
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            ClientId = clientId,
            CreePar = creePar,
            Numero = numero,
            Reference = reference?.Trim(),
            Statut = StatutFacture.Brouillon,
            TypeFacture = typeFacture,
            ModePaiement = modePaiement,
            Devise = devise,
            DateEmission = DateTime.UtcNow,
            DateEcheance = dateEcheance,
            Notes = notes?.Trim(),
            ConditionsPaiement = conditionsPaiement?.Trim(),
            FactureOrigineId = factureOrigineId,
            TotalHt = 0,
            TotalTva = 0,
            TotalTtc = 0,
            MontantPaye = 0,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };

        facture._historique.Add(HistoriqueFacture.Creer(
            facture.Id, creePar, "Création", "Facture créée en brouillon."));

        return facture;
    }

    public void AjouterLigne(LigneFacture ligne)
    {
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Impossible de modifier une facture hors brouillon.");

        _lignes.Add(ligne);
        RecalculerTotaux();
        ModifieLe = DateTime.UtcNow;
    }

    public void SupprimerLigne(Guid ligneId)
    {
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Impossible de modifier une facture hors brouillon.");

        var ligne = _lignes.FirstOrDefault(l => l.Id == ligneId)
            ?? throw new NotFoundException("Ligne introuvable.");
        _lignes.Remove(ligne);
        RecalculerTotaux();
        ModifieLe = DateTime.UtcNow;
    }

    public void MettreAJourInfos(
        ModePaiement modePaiement, DateTime dateEcheance,
        string? reference, string? notes, string? conditionsPaiement)
    {
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Impossible de modifier une facture hors brouillon.");

        if (dateEcheance.Date < DateEmission.Date)
            throw new ValidationMetierException("La date d'échéance doit être postérieure à la date d'émission.");

        ModePaiement = modePaiement;
        DateEcheance = dateEcheance;
        Reference = reference?.Trim();
        Notes = notes?.Trim();
        ConditionsPaiement = conditionsPaiement?.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void ValiderMetier(Guid validePar)
    {
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Seul un brouillon peut être validé.");

        if (!_lignes.Any())
            throw new ValidationMetierException("La facture doit contenir au moins une ligne.");

        if (TotalTtc <= 0)
            throw new ValidationMetierException("Le montant total doit être positif.");

        Statut = StatutFacture.Validee;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, validePar, "Validation",
            $"Facture validée. Total TTC : {TotalTtc:N3} {Devise}."));
    }

    public void MarquerConforme(Guid validePar, string versionTeif)
    {
        if (Statut != StatutFacture.Validee)
            throw new ValidationMetierException("Seule une facture validée peut être marquée conforme.");

        Statut = StatutFacture.Conforme;
        VersionTeif = versionTeif;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, validePar, "Conformité TEIF",
            $"Facture marquée conforme TEIF version {versionTeif}."));
    }

    public void EnregistrerXml(string xmlContent, string hashIntegrite)
    {
        XmlTeif = xmlContent;
        HashIntegrite = hashIntegrite;
        ModifieLe = DateTime.UtcNow;
    }

    public void MarquerTransmise(Guid validePar)
    {
        if (Statut != StatutFacture.Conforme)
            throw new ValidationMetierException("Seule une facture conforme peut être transmise.");

        Statut = StatutFacture.Transmise;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, validePar, "Transmission", "Facture transmise au système TTN."));
    }

    public void MarquerAcceptee(Guid validePar)
    {
        if (Statut != StatutFacture.Transmise)
            throw new ValidationMetierException("Seule une facture transmise peut être acceptée.");

        Statut = StatutFacture.Acceptee;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, validePar, "Acceptation TTN", "Facture acceptée par le système TTN."));
    }

    public void MarquerRejetee(Guid validePar, string motif)
    {
        if (Statut != StatutFacture.Transmise && Statut != StatutFacture.Conforme)
            throw new ValidationMetierException("Statut invalide pour un rejet.");

        Statut = StatutFacture.Rejetee;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, validePar, "Rejet", $"Facture rejetée. Motif : {motif}"));
    }

    public void Annuler(Guid annulerPar, string motif)
    {
        if (Statut == StatutFacture.Payee || Statut == StatutFacture.Annulee)
            throw new ValidationMetierException("Cette facture ne peut pas être annulée.");

        Statut = StatutFacture.Annulee;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, annulerPar, "Annulation", $"Facture annulée. Motif : {motif}"));
    }

    public void EnregistrerPaiement(decimal montant, Guid enregistrePar)
    {
        if (Statut != StatutFacture.Acceptee && Statut != StatutFacture.PartiellemementPayee)
            throw new ValidationMetierException("La facture doit être acceptée pour enregistrer un paiement.");

        if (montant <= 0)
            throw new ValidationMetierException("Le montant du paiement doit être positif.");

        MontantPaye = Math.Round(MontantPaye + montant, 3);

        if (MontantPaye >= TotalTtc)
        {
            MontantPaye = TotalTtc;
            Statut = StatutFacture.Payee;
            DatePaiement = DateTime.UtcNow;
            _historique.Add(HistoriqueFacture.Creer(
                Id, enregistrePar, "Paiement complet",
                $"Paiement total reçu : {TotalTtc:N3} {Devise}."));
        }
        else
        {
            Statut = StatutFacture.PartiellemementPayee;
            _historique.Add(HistoriqueFacture.Creer(
                Id, enregistrePar, "Paiement partiel",
                $"Paiement de {montant:N3} {Devise} reçu. Reste : {TotalTtc - MontantPaye:N3} {Devise}."));
        }

        ModifieLe = DateTime.UtcNow;
    }

    public void RemettreBrouillon(Guid remisePar)
    {
        if (Statut != StatutFacture.Rejetee)
            throw new ValidationMetierException("Seule une facture rejetée peut être remise en brouillon.");

        Statut = StatutFacture.Brouillon;
        XmlTeif = null;
        HashIntegrite = null;
        ModifieLe = DateTime.UtcNow;

        _historique.Add(HistoriqueFacture.Creer(
            Id, remisePar, "Remise en brouillon", "Facture remise en brouillon après rejet."));
    }

    private void RecalculerTotaux()
    {
        TotalHt = Math.Round(_lignes.Sum(l => l.MontantHt), 3);
        TotalTva = Math.Round(_lignes.Sum(l => l.MontantTva), 3);
        TotalTtc = Math.Round(TotalHt + TotalTva, 3);
    }

    public decimal MontantRestant => Math.Round(TotalTtc - MontantPaye, 3);
    public bool EstEnRetard => DateEcheance.Date < DateTime.UtcNow.Date
        && Statut != StatutFacture.Payee
        && Statut != StatutFacture.Annulee;
}
