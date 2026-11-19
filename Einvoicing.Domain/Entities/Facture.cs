using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Facture
{
    /// <summary>Droit de timbre sur les factures (Tunisie) : 1,000 TND par facture.</summary>
    public const decimal MontantTimbreFiscal = 1.000m;

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
    /// <summary>Remise globale en montant HT, répartie au prorata sur les bases de TVA.</summary>
    public decimal RemiseGlobale { get; private set; }
    /// <summary>Droit de timbre appliqué (hors TVA, hors base de retenue à la source).</summary>
    public decimal TimbreFiscal { get; private set; }
    public decimal MontantPaye { get; private set; }
    public bool AppliquerRS { get; private set; }
    public string? CodeRS { get; private set; }
    public decimal TauxRS { get; private set; }
    public decimal BaseRS { get; private set; }
    public decimal MontantRS { get; private set; }
    public decimal NetAPayer { get; private set; }

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
        bool appliquerRS = false,
        string? codeRS = null,
        decimal tauxRS = 0,
        Guid? factureOrigineId = null,
        decimal remiseGlobale = 0,
        bool appliquerTimbre = true)
    {
        if (remiseGlobale < 0)
            throw new ValidationMetierException("La remise globale ne peut pas être négative.");

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
            AppliquerRS = appliquerRS,
            CodeRS = string.IsNullOrWhiteSpace(codeRS) ? null : codeRS.Trim(),
            TauxRS = appliquerRS ? Math.Round(tauxRS, 3, MidpointRounding.AwayFromZero) : 0,
            FactureOrigineId = factureOrigineId,
            RemiseGlobale = Math.Round(remiseGlobale, 3, MidpointRounding.AwayFromZero),
            TimbreFiscal = typeFacture == TypeFacture.Facture && appliquerTimbre ? MontantTimbreFiscal : 0,
            TotalHt = 0,
            TotalTva = 0,
            TotalTtc = 0,
            MontantPaye = 0,
            NetAPayer = typeFacture == TypeFacture.Facture && appliquerTimbre ? MontantTimbreFiscal : 0,
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };

        facture._historique.Add(HistoriqueFacture.Creer(
            facture.Id, creePar, "Création", "Facture créée en brouillon."));

        return facture;
    }

    /// <summary>
    /// Numéro définitif, attribué une fois tous les contrôles passés : un document refusé
    /// ne consomme pas de numéro (numérotation continue, sans trou).
    /// </summary>
    public void AttribuerNumero(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero)) throw new ValidationMetierException("Le numéro est requis.");
        if (Statut != StatutFacture.Brouillon) throw new ValidationMetierException("Le numéro d'une facture émise ne peut pas changer.");
        Numero = numero.Trim();
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
        string? reference, string? notes, string? conditionsPaiement,
        bool appliquerRS = false, string? codeRS = null, decimal tauxRS = 0,
        decimal remiseGlobale = 0, bool appliquerTimbre = true)
    {
        if (remiseGlobale < 0)
            throw new ValidationMetierException("La remise globale ne peut pas être négative.");
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Impossible de modifier une facture hors brouillon.");

        if (dateEcheance.Date < DateEmission.Date)
            throw new ValidationMetierException("La date d'échéance doit être postérieure à la date d'émission.");

        ModePaiement = modePaiement;
        DateEcheance = dateEcheance;
        Reference = reference?.Trim();
        Notes = notes?.Trim();
        ConditionsPaiement = conditionsPaiement?.Trim();
        AppliquerRS = appliquerRS;
        CodeRS = string.IsNullOrWhiteSpace(codeRS) ? null : codeRS.Trim();
        TauxRS = appliquerRS ? Math.Round(tauxRS, 3, MidpointRounding.AwayFromZero) : 0;
        RemiseGlobale = Math.Round(remiseGlobale, 3, MidpointRounding.AwayFromZero);
        TimbreFiscal = TypeFacture == TypeFacture.Facture && appliquerTimbre ? MontantTimbreFiscal : 0;
        RecalculerTotaux();
        ModifieLe = DateTime.UtcNow;
    }

    /// <summary>Vérifie que la remise globale ne dépasse pas le total HT des lignes.</summary>
    public void ControlerRemiseGlobale()
    {
        var totalLignesHt = _lignes.Sum(l => l.MontantHt);
        if (RemiseGlobale > totalLignesHt)
            throw new ValidationMetierException("La remise globale ne peut pas dépasser le total HT des lignes.");
    }

    public void ValiderMetier(Guid validePar)
    {
        if (Statut != StatutFacture.Brouillon)
            throw new ValidationMetierException("Seul un brouillon peut être validé.");

        if (!_lignes.Any())
            throw new ValidationMetierException("La facture doit contenir au moins une ligne.");

        if (TotalTtc <= 0)
            throw new ValidationMetierException("Le montant total doit être positif.");

        ControlerRemiseGlobale();

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

        montant = Math.Round(montant, 3, MidpointRounding.AwayFromZero);
        if (montant > MontantRestant)
            throw new ValidationMetierException(
                $"Le montant ({montant:N3}) dépasse le solde restant ({MontantRestant:N3}).");

        MontantPaye = Math.Round(MontantPaye + montant, 3, MidpointRounding.AwayFromZero);

        if (MontantPaye >= NetAPayer)
        {
            Statut = StatutFacture.Payee;
            DatePaiement = DateTime.UtcNow;
            _historique.Add(HistoriqueFacture.Creer(
                Id, enregistrePar, "Paiement complet",
                $"Paiement total reçu : {NetAPayer:N3} {Devise}."));
        }
        else
        {
            Statut = StatutFacture.PartiellemementPayee;
            _historique.Add(HistoriqueFacture.Creer(
                Id, enregistrePar, "Paiement partiel",
                $"Paiement de {montant:N3} {Devise} reçu. Reste : {NetAPayer - MontantPaye:N3} {Devise}."));
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

    /// <summary>
    /// Calcul fiscal tunisien :
    /// HT net = somme des HT lignes - remise globale (répartie au prorata sur chaque taux de TVA) ;
    /// TTC = HT net + TVA ; retenue à la source calculée sur le TTC (art. 52 du Code de l'IRPP et de l'IS) ;
    /// net à payer = TTC + droit de timbre - retenue à la source.
    /// </summary>
    private void RecalculerTotaux()
    {
        var totalLignesHt = _lignes.Sum(l => l.MontantHt);
        var remise = Math.Min(RemiseGlobale, totalLignesHt);
        var ratio = totalLignesHt > 0 ? (totalLignesHt - remise) / totalLignesHt : 1m;

        TotalHt = Math.Round(totalLignesHt - remise, 3, MidpointRounding.AwayFromZero);
        TotalTva = Math.Round(_lignes.Sum(l => Math.Round(l.MontantTva * ratio, 3, MidpointRounding.AwayFromZero)), 3, MidpointRounding.AwayFromZero);
        TotalTtc = Math.Round(TotalHt + TotalTva, 3, MidpointRounding.AwayFromZero);
        BaseRS = AppliquerRS ? TotalTtc : 0;
        MontantRS = AppliquerRS ? Math.Round(BaseRS * TauxRS / 100m, 3, MidpointRounding.AwayFromZero) : 0;
        NetAPayer = Math.Round(Math.Max(0, TotalTtc + TimbreFiscal - MontantRS), 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Ventilation par taux de TVA après répartition de la remise globale
    /// (bases et montants identiques au calcul des totaux).
    /// </summary>
    public IReadOnlyList<VentilationTva> VentilerTva()
    {
        var totalLignesHt = _lignes.Sum(l => l.MontantHt);
        var remise = Math.Min(RemiseGlobale, totalLignesHt);
        var ratio = totalLignesHt > 0 ? (totalLignesHt - remise) / totalLignesHt : 1m;
        return _lignes
            .GroupBy(l => l.TauxTva)
            .OrderByDescending(g => g.Key)
            .Select(g => new VentilationTva(
                g.Key,
                Math.Round(g.Sum(l => l.MontantHt * ratio), 3, MidpointRounding.AwayFromZero),
                Math.Round(g.Sum(l => Math.Round(l.MontantTva * ratio, 3, MidpointRounding.AwayFromZero)), 3, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public decimal MontantRestant => Math.Round(NetAPayer - MontantPaye, 3, MidpointRounding.AwayFromZero);
    public bool EstEnRetard => DateEcheance.Date < DateTime.UtcNow.Date
        && Statut != StatutFacture.Payee
        && Statut != StatutFacture.Annulee;
}

public sealed record VentilationTva(decimal Taux, decimal BaseHt, decimal MontantTva);
