using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class CompteComptable
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string Intitule { get; private set; } = string.Empty;
    public string Classe { get; private set; } = string.Empty;
    public string Nature { get; private set; } = string.Empty;
    public string Usage { get; private set; } = string.Empty;
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }

    private CompteComptable() { }

    public static CompteComptable Creer(Guid entrepriseId, string numero, string intitule, string classe, string nature, string usage)
    {
        if (string.IsNullOrWhiteSpace(numero) || numero.Trim().Length != 6 || !numero.Trim().All(char.IsDigit))
            throw new ValidationMetierException("Le numéro de compte doit contenir six chiffres.");
        if (string.IsNullOrWhiteSpace(intitule)) throw new ValidationMetierException("L'intitulé du compte est obligatoire.");
        return new CompteComptable
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, Numero = numero.Trim(), Intitule = intitule.Trim(),
            Classe = classe.Trim(), Nature = nature.Trim(), Usage = usage.Trim(), EstActif = true, CreeLe = DateTime.UtcNow
        };
    }
}

public sealed class EcritureComptable
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid GroupeId { get; private set; }
    public DateTime Date { get; private set; }
    public string Journal { get; private set; } = string.Empty;
    public string Piece { get; private set; } = string.Empty;
    public string Compte { get; private set; } = string.Empty;
    public string Libelle { get; private set; } = string.Empty;
    public string? Tiers { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string Statut { get; private set; } = "Validée";
    public string Source { get; private set; } = "Manuelle";
    public Guid CreePar { get; private set; }
    public DateTime CreeLe { get; private set; }

    private EcritureComptable() { }

    public static IReadOnlyList<EcritureComptable> CreerPiece(Guid entrepriseId, Guid creePar, DateTime date, string journal,
        string piece, string libelle, string? tiers, IEnumerable<(string Compte, decimal Debit, decimal Credit)> lignes)
    {
        var normalized = lignes.Select(line => (line.Compte.Trim(), Math.Round(line.Debit, 3), Math.Round(line.Credit, 3))).ToList();
        if (normalized.Count < 2) throw new ValidationMetierException("Une écriture doit contenir au moins deux lignes.");
        if (normalized.Any(line => line.Item1.Length != 6 || !line.Item1.All(char.IsDigit) || (line.Item2 <= 0 && line.Item3 <= 0) || (line.Item2 > 0 && line.Item3 > 0)))
            throw new ValidationMetierException("Chaque ligne doit comporter un compte et un débit ou un crédit.");
        if (Math.Abs(normalized.Sum(line => line.Item2) - normalized.Sum(line => line.Item3)) >= 0.001m)
            throw new ValidationMetierException("Le total débit doit être égal au total crédit.");

        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        return normalized.Select(line => new EcritureComptable
        {
            Id = Guid.NewGuid(), GroupeId = groupId, EntrepriseId = entrepriseId, CreePar = creePar,
            Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), Journal = string.IsNullOrWhiteSpace(journal) ? "OD" : journal.Trim().ToUpperInvariant(),
            Piece = piece.Trim(), Compte = line.Item1, Libelle = libelle.Trim(), Tiers = tiers?.Trim(),
            Debit = line.Item2, Credit = line.Item3, CreeLe = now
        }).ToList();
    }
}

public sealed class ActifComptable
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Libelle { get; private set; } = string.Empty;
    public string Famille { get; private set; } = string.Empty;
    public string Compte { get; private set; } = string.Empty;
    public decimal TauxAmortissement { get; private set; }
    public decimal ValeurAcquisition { get; private set; }
    public DateTime DateAcquisition { get; private set; }
    public string Statut { get; private set; } = "En service";
    public DateTime CreeLe { get; private set; }

    private ActifComptable() { }

    public static ActifComptable Creer(Guid entrepriseId, string libelle, string famille, string compte, decimal tauxAmortissement,
        decimal valeurAcquisition, DateTime dateAcquisition, string? statut)
    {
        if (string.IsNullOrWhiteSpace(libelle) || valeurAcquisition <= 0 || tauxAmortissement <= 0 || tauxAmortissement > 100)
            throw new ValidationMetierException("Les données de l'immobilisation sont invalides.");
        return new ActifComptable
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, Libelle = libelle.Trim(), Famille = famille.Trim(), Compte = compte.Trim(),
            TauxAmortissement = Math.Round(tauxAmortissement, 3), ValeurAcquisition = Math.Round(valeurAcquisition, 3),
            DateAcquisition = DateTime.SpecifyKind(dateAcquisition.Date, DateTimeKind.Utc), Statut = string.IsNullOrWhiteSpace(statut) ? "En service" : statut.Trim(), CreeLe = DateTime.UtcNow
        };
    }

    public void ChangerTaux(decimal taux)
    {
        if (taux <= 0 || taux > 100) throw new ValidationMetierException("Le taux d'amortissement est invalide.");
        TauxAmortissement = Math.Round(taux, 3);
    }
}

public sealed class ExerciceComptable
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public int Annee { get; private set; }
    public DateTime DateDebut { get; private set; }
    public DateTime DateFin { get; private set; }
    public string Statut { get; private set; } = "Ouvert";
    public DateTime CreeLe { get; private set; }
    public DateTime? ClotureLe { get; private set; }

    private ExerciceComptable() { }

    public static ExerciceComptable Creer(Guid entrepriseId, int annee)
    {
        if (annee is < 2000 or > 2100) throw new ValidationMetierException("L'année d'exercice est invalide.");
        return new ExerciceComptable
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, Annee = annee,
            DateDebut = new DateTime(annee, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateFin = new DateTime(annee, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreeLe = DateTime.UtcNow
        };
    }

    public void Cloturer() { if (Statut == "Clôturé") return; Statut = "Clôturé"; ClotureLe = DateTime.UtcNow; }
}
