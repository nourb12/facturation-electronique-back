using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class EmployePaie
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Matricule { get; private set; } = string.Empty;
    public string Nom { get; private set; } = string.Empty;
    public string Poste { get; private set; } = string.Empty;
    public string Departement { get; private set; } = string.Empty;
    public string? Cin { get; private set; }
    public string? NumeroCnss { get; private set; }
    public string? Rib { get; private set; }
    public decimal SalaireBase { get; private set; }
    public decimal PrimeMensuelle { get; private set; }
    public decimal AvantageMensuel { get; private set; }
    public bool EstActif { get; private set; }
    public DateTime CreeLe { get; private set; }

    private EmployePaie() { }

    public static EmployePaie Creer(Guid entrepriseId, string matricule, string nom, string poste, string departement,
        decimal salaireBase, decimal primeMensuelle, decimal avantageMensuel, string? cin, string? numeroCnss, string? rib)
    {
        if (string.IsNullOrWhiteSpace(matricule) || string.IsNullOrWhiteSpace(nom) || salaireBase < 0)
            throw new ValidationMetierException("Les informations de l'employé sont invalides.");
        return new EmployePaie
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, Matricule = matricule.Trim(), Nom = nom.Trim(), Poste = poste.Trim(), Departement = departement.Trim(),
            SalaireBase = Math.Round(salaireBase, 3), PrimeMensuelle = Math.Round(Math.Max(0, primeMensuelle), 3), AvantageMensuel = Math.Round(Math.Max(0, avantageMensuel), 3),
            Cin = cin?.Trim(), NumeroCnss = numeroCnss?.Trim(), Rib = rib?.Trim(), EstActif = true, CreeLe = DateTime.UtcNow
        };
    }
}

public sealed class BulletinPaie
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid EmployeId { get; private set; }
    public string Periode { get; private set; } = string.Empty;
    public string Statut { get; private set; } = "Brouillon";
    public decimal Brut { get; private set; }
    public decimal CnssSalarie { get; private set; }
    public decimal Irpp { get; private set; }
    public decimal Avances { get; private set; }
    public decimal NetAPayer { get; private set; }
    public DateTime? DatePaiement { get; private set; }
    public string? ReferencePaiement { get; private set; }
    public DateTime CreeLe { get; private set; }

    private BulletinPaie() { }

    public static BulletinPaie Generer(EmployePaie employe, string periode)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(periode, @"^\d{4}-\d{2}$")) throw new ValidationMetierException("La période doit être au format AAAA-MM.");
        var brut = Math.Round(employe.SalaireBase + employe.PrimeMensuelle + employe.AvantageMensuel, 3);
        var cnss = Math.Round(brut * 0.0918m, 3);
        var irpp = Math.Round(Math.Max(0, brut - cnss - 600m) * 0.12m, 3);
        return new BulletinPaie
        {
            Id = Guid.NewGuid(), EntrepriseId = employe.EntrepriseId, EmployeId = employe.Id, Periode = periode,
            Brut = brut, CnssSalarie = cnss, Irpp = irpp, NetAPayer = Math.Round(brut - cnss - irpp, 3), CreeLe = DateTime.UtcNow
        };
    }

    public void Valider() { if (Statut != "Brouillon") throw new ValidationMetierException("Seul un bulletin brouillon peut être validé."); Statut = "Validée"; }
    public void Payer(DateTime datePaiement, string? reference)
    {
        if (Statut != "Validée") throw new ValidationMetierException("Le bulletin doit être validé avant paiement.");
        Statut = "Payée"; DatePaiement = DateTime.SpecifyKind(datePaiement.Date, DateTimeKind.Utc); ReferencePaiement = reference?.Trim();
    }
}
