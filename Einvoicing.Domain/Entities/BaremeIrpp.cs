namespace Einvoicing.Domain.Entities;

/// <summary>
/// Retenue IRPP mensuelle sur salaires (Tunisie, barème en vigueur depuis la loi de finances 2025).
/// Méthode : on annualise le salaire imposable, on applique l'abattement pour frais professionnels
/// (10 % plafonné à 2 000 TND par an), puis le barème progressif annuel ; la retenue du mois est le douzième.
/// Les déductions pour charges de famille (art. 40 du Code de l'IRPP et de l'IS) sont retranchées
/// du revenu imposable annuel ; la contribution sociale de solidarité (CSS) est calculée sur ce même revenu.
/// </summary>
public static class BaremeIrpp
{
    /// <summary>Cotisation salariale CNSS (régime général) depuis janvier 2025 : 9,68 %.</summary>
    public const decimal TauxCnssSalarie = 0.0968m;
    /// <summary>Cotisation patronale CNSS (régime général) depuis janvier 2025 : 17,07 %.</summary>
    public const decimal TauxCnssPatronal = 0.1707m;

    // Déductions annuelles pour charges de famille.
    public const decimal DeductionChefDeFamille = 300m;
    public const decimal DeductionParEnfant = 100m;
    public const decimal DeductionEnfantEtudiant = 1000m;
    public const decimal DeductionEnfantHandicape = 2000m;
    public const int NombreMaxEnfants = 4;

    /// <summary>
    /// CSS sur salaires : 0,5 % du revenu imposable (taux réduit LF 2023, art. 22, prorogé),
    /// exonération si le revenu net annuel après déductions ne dépasse pas 5 000 TND (LF 2020, art. 39).
    /// Le taux est paramétrable (configuration Paie:TauxCss) pour suivre les lois de finances.
    /// </summary>
    public const decimal TauxCssParDefaut = 0.005m;
    public const decimal SeuilExonerationCss = 5000m;
    public const decimal TauxFraisProfessionnels = 0.10m;
    public const decimal PlafondFraisProfessionnels = 2000m;

    /// <summary>Tranches annuelles : (borne supérieure, taux marginal).</summary>
    private static readonly (decimal Plafond, decimal Taux)[] Tranches =
    [
        (5_000m, 0.00m),
        (10_000m, 0.15m),
        (20_000m, 0.25m),
        (30_000m, 0.30m),
        (40_000m, 0.33m),
        (50_000m, 0.36m),
        (70_000m, 0.38m),
        (decimal.MaxValue, 0.40m),
    ];

    public static decimal ImpotAnnuel(decimal revenuImposableAnnuel)
    {
        if (revenuImposableAnnuel <= 0) return 0;
        decimal impot = 0, plancher = 0;
        foreach (var (plafond, taux) in Tranches)
        {
            if (revenuImposableAnnuel <= plancher) break;
            impot += (Math.Min(revenuImposableAnnuel, plafond) - plancher) * taux;
            plancher = plafond;
        }
        return Math.Round(impot, 3, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Déductions annuelles : chef de famille, puis les 4 premiers enfants à charge
    /// (étudiants non boursiers comptés en priorité), enfants handicapés sans limite de nombre.
    /// </summary>
    public static decimal DeductionsFamiliales(SituationFamiliale situation)
    {
        var etudiants = Math.Min(Math.Max(0, situation.EnfantsEtudiants), NombreMaxEnfants);
        var autres = Math.Min(Math.Max(0, situation.EnfantsACharge), NombreMaxEnfants - etudiants);
        return (situation.ChefDeFamille ? DeductionChefDeFamille : 0)
            + etudiants * DeductionEnfantEtudiant
            + autres * DeductionParEnfant
            + Math.Max(0, situation.EnfantsHandicapes) * DeductionEnfantHandicape;
    }

    /// <summary>Revenu imposable annuel = (brut - CNSS) x 12 - frais professionnels - charges de famille.</summary>
    public static decimal RevenuImposableAnnuel(decimal brutMensuel, decimal cnssMensuelle, SituationFamiliale? situation = null)
    {
        var netSocialAnnuel = Math.Max(0, brutMensuel - cnssMensuelle) * 12m;
        var abattement = Math.Min(netSocialAnnuel * TauxFraisProfessionnels, PlafondFraisProfessionnels);
        var deductions = situation is null ? 0 : DeductionsFamiliales(situation);
        return Math.Max(0, netSocialAnnuel - abattement - deductions);
    }

    public static decimal RetenueMensuelle(decimal brutMensuel, decimal cnssMensuelle, SituationFamiliale? situation = null)
    {
        var impotAnnuel = ImpotAnnuel(RevenuImposableAnnuel(brutMensuel, cnssMensuelle, situation));
        return Math.Round(impotAnnuel / 12m, 3, MidpointRounding.AwayFromZero);
    }

    public static decimal CssMensuelle(decimal brutMensuel, decimal cnssMensuelle, SituationFamiliale? situation = null, decimal? tauxCss = null)
    {
        var imposable = RevenuImposableAnnuel(brutMensuel, cnssMensuelle, situation);
        if (imposable <= SeuilExonerationCss) return 0;
        var taux = Math.Max(0, tauxCss ?? TauxCssParDefaut);
        return Math.Round(imposable * taux / 12m, 3, MidpointRounding.AwayFromZero);
    }
}

/// <summary>Situation familiale de l'employé, utilisée pour les déductions IRPP.</summary>
public sealed record SituationFamiliale(bool ChefDeFamille, int EnfantsACharge, int EnfantsEtudiants, int EnfantsHandicapes)
{
    public static readonly SituationFamiliale Aucune = new(false, 0, 0, 0);
}
