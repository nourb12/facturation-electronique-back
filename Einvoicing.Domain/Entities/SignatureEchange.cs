using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;





public sealed class SignatureRequest
{
    public Guid Id { get; private set; }
    public Guid FactureId { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid DemandeePar { get; private set; }

    public StatutSignature Statut { get; private set; }
    public string? SignatureValue { get; private set; }
    public string? CertificatId { get; private set; }
    public string? MessageErreur { get; private set; }
    public int NbTentatives { get; private set; }

    public DateTime CreeLe { get; private set; }
    public DateTime? SigneeA { get; private set; }
    public DateTime? EchoueeA { get; private set; }

    private SignatureRequest() { }

    public static SignatureRequest Creer(Guid factureId, Guid entrepriseId, Guid demandeePar)
    {
        return new SignatureRequest
        {
            Id = Guid.NewGuid(),
            FactureId = factureId,
            EntrepriseId = entrepriseId,
            DemandeePar = demandeePar,
            Statut = StatutSignature.EnAttente,
            NbTentatives = 0,
            CreeLe = DateTime.UtcNow
        };
    }

    public void Signer(string signatureValue, string certificatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureValue);
        Statut = StatutSignature.Signee;
        SignatureValue = signatureValue;
        CertificatId = certificatId;
        SigneeA = DateTime.UtcNow;
    }

    public void Echouer(string messageErreur)
    {
        NbTentatives++;
        MessageErreur = messageErreur;

        if (NbTentatives >= 3)
            Statut = StatutSignature.EchecDefinitif;
        else
            Statut = StatutSignature.Echec;

        EchoueeA = DateTime.UtcNow;
    }

    public void Relancer()
    {
        if (Statut == StatutSignature.EchecDefinitif)
            throw new ValidationMetierException("Signature en échec définitif — impossible de relancer.");

        Statut = StatutSignature.EnAttente;
        MessageErreur = null;
    }

    public bool EstSignee => Statut == StatutSignature.Signee;
}





public sealed class ExternalExchange
{
    public Guid Id { get; private set; }
    public Guid FactureId { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid EnvoyePar { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;
    public StatutEchange Statut { get; private set; }
    public string? ReponseCode { get; private set; }
    public string? ReponseMessage { get; private set; }
    public string? MotifRejet { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? DerniereAttempteLe { get; private set; }

    public DateTime CreeLe { get; private set; }
    public DateTime? AccepteeA { get; private set; }
    public DateTime? RejeteeLe { get; private set; }

    private ExternalExchange() { }

    public static ExternalExchange Creer(Guid factureId, Guid entrepriseId, Guid envoyePar)
    {
        return new ExternalExchange
        {
            Id = Guid.NewGuid(),
            FactureId = factureId,
            EntrepriseId = entrepriseId,
            EnvoyePar = envoyePar,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Statut = StatutEchange.EnAttente,
            RetryCount = 0,
            CreeLe = DateTime.UtcNow
        };
    }

    public void MarquerEnvoye()
    {
        Statut = StatutEchange.Envoye;
        DerniereAttempteLe = DateTime.UtcNow;
    }

    public void MarquerAcknowledge()
    {
        Statut = StatutEchange.Acknowledge;
    }

    public void MarquerAccepte()
    {
        Statut = StatutEchange.Accepte;
        AccepteeA = DateTime.UtcNow;
        ReponseCode = "200";
    }

    public void MarquerRejete(string code, string message, string? motif = null)
    {
        Statut = StatutEchange.Rejete;
        ReponseCode = code;
        ReponseMessage = message;
        MotifRejet = motif;
        RejeteeLe = DateTime.UtcNow;
    }

    public void MarquerErreur(string message)
    {
        RetryCount++;
        ReponseMessage = message;

        Statut = RetryCount >= 3
            ? StatutEchange.EchecDefinitif
            : StatutEchange.Erreur;

        DerniereAttempteLe = DateTime.UtcNow;
    }

    public bool PeutEtreRelance =>
        Statut == StatutEchange.Erreur && RetryCount < 3;
}
