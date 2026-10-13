using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class EvenementAudit
{
    public Guid Id { get; private set; }
    public Guid? EntrepriseId { get; private set; }
    public Guid? UtilisateurId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Ressource { get; private set; } = string.Empty;
    public string? RessourceId { get; private set; }
    public string? Details { get; private set; }
    public string AdresseIp { get; private set; } = string.Empty;
    public string? AgentUtilisateur { get; private set; }
    public int CodeReponse { get; private set; }
    public DateTime CreeLe { get; private set; }

    private EvenementAudit() { }

    public static EvenementAudit Creer(Guid? entrepriseId, Guid? utilisateurId, string action, string ressource,
        string? ressourceId, string? details, string? adresseIp, string? agentUtilisateur, int codeReponse)
        => new()
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, UtilisateurId = utilisateurId,
            Action = action.Trim(), Ressource = ressource.Trim(), RessourceId = ressourceId,
            Details = details, AdresseIp = string.IsNullOrWhiteSpace(adresseIp) ? "Inconnue" : adresseIp.Trim(),
            AgentUtilisateur = string.IsNullOrWhiteSpace(agentUtilisateur) ? null : agentUtilisateur.Trim(),
            CodeReponse = codeReponse, CreeLe = DateTime.UtcNow
        };
}

public sealed class AbonnementSaas
{
    private static readonly HashSet<string> StatutsAutorises = ["Actif", "A_regler", "Suspendu", "Annule"];

    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Plan { get; private set; } = string.Empty;
    public string Statut { get; private set; } = "Actif";
    public decimal PrixMensuel { get; private set; }
    public int LimiteUtilisateurs { get; private set; }
    public int LimiteFacturesMensuelles { get; private set; }
    public DateTime DateDebut { get; private set; }
    public DateTime ProchainRenouvellement { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private AbonnementSaas() { }

    public static AbonnementSaas Creer(Guid entrepriseId, string plan, decimal prixMensuel, int limiteUtilisateurs,
        int limiteFacturesMensuelles, DateTime? prochainRenouvellement = null)
    {
        if (string.IsNullOrWhiteSpace(plan)) throw new ValidationMetierException("Le nom du plan est obligatoire.");
        if (prixMensuel < 0 || limiteUtilisateurs < 1 || limiteFacturesMensuelles < 1)
            throw new ValidationMetierException("Les paramètres de l'abonnement sont invalides.");

        var now = DateTime.UtcNow;
        return new AbonnementSaas
        {
            Id = Guid.NewGuid(), EntrepriseId = entrepriseId, Plan = plan.Trim(), PrixMensuel = Math.Round(prixMensuel, 3),
            LimiteUtilisateurs = limiteUtilisateurs, LimiteFacturesMensuelles = limiteFacturesMensuelles,
            DateDebut = now, ProchainRenouvellement = prochainRenouvellement?.ToUniversalTime() ?? now.AddMonths(1),
            CreeLe = now, ModifieLe = now
        };
    }

    public void ChangerStatut(string statut)
    {
        if (!StatutsAutorises.Contains(statut)) throw new ValidationMetierException("Le statut d'abonnement est invalide.");
        Statut = statut;
        ModifieLe = DateTime.UtcNow;
    }
}
