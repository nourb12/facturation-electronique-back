using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class Personnalisation
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string DonneesJson { get; private set; } = "{}";
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private Personnalisation() { }

    public static Personnalisation Creer(Guid entrepriseId, string donneesJson)
    {
        if (string.IsNullOrWhiteSpace(donneesJson))
            throw new ValidationMetierException("Les données de personnalisation sont obligatoires.");

        return new Personnalisation
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            DonneesJson = donneesJson.Trim(),
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void MettreAJour(string donneesJson)
    {
        if (string.IsNullOrWhiteSpace(donneesJson))
            throw new ValidationMetierException("Les données de personnalisation sont obligatoires.");

        DonneesJson = donneesJson.Trim();
        ModifieLe = DateTime.UtcNow;
    }
}