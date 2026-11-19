using System.Security.Cryptography;
using System.Text;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class InvitationUtilisateur
{
    public Guid Id { get; private set; }
    public Guid UtilisateurId { get; private set; }
    public Guid InvitePar { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTime CreeLe { get; private set; }
    public DateTime ExpireLe { get; private set; }
    public DateTime? AccepteeLe { get; private set; }
    public DateTime? EmailEnvoyeLe { get; private set; }
    private InvitationUtilisateur() { }
    public static InvitationUtilisateur Creer(Guid utilisateurId, Guid acteur, string token) => new()
    {
        Id = Guid.NewGuid(), UtilisateurId = utilisateurId, InvitePar = acteur,
        TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
        CreeLe = DateTime.UtcNow, ExpireLe = DateTime.UtcNow.AddHours(48)
    };
    public bool EstValide(string token) => AccepteeLe is null && ExpireLe > DateTime.UtcNow &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(TokenHash), SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public void Accepter(string token)
    {
        if (!EstValide(token)) throw new ValidationMetierException("Invitation expiree, invalide ou deja utilisee.");
        AccepteeLe = DateTime.UtcNow;
    }
    public void Annuler() => ExpireLe = DateTime.UtcNow;
    public void MarquerEmailEnvoye() => EmailEnvoyeLe = DateTime.UtcNow;
}
