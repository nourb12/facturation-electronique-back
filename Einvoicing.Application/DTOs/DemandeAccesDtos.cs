using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.DTOs;

public sealed class SoumettreDemandeAccesRequest
{
    /// <summary>Parcours choisi dans le formulaire: independant ou entreprise.</summary>
    public string ProfileType { get; set; } = string.Empty;
    public string RaisonSociale { get; set; } = string.Empty;
    public string MatriculeFiscal { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string FormeJuridique { get; set; } = string.Empty;
    public string NomEntreprise { get; set; } = string.Empty;
    public string Adresse { get; set; } = string.Empty;
    public string Gouvernorat { get; set; } = string.Empty;
    public string CodePostal { get; set; } = string.Empty;
    public string? SiteWeb { get; set; }
    public string DevisePrincipale { get; set; } = "TND";
    public string TelEntreprise { get; set; } = string.Empty;
    public string RespPrenom { get; set; } = string.Empty;
    public string RespNom { get; set; } = string.Empty;
    public string RespFonction { get; set; } = string.Empty;
    public string? RespFonctionAutre { get; set; }
    public string? RespEmail { get; set; }
    public string RespTel { get; set; } = string.Empty;

    public IFormFile? Logo { get; set; }
    public IFormFile? RegistreCommerce { get; set; }
    public IFormFile? Patente { get; set; }
    public IFormFile? CinResponsable { get; set; }
    public IFormFile? Rib { get; set; }
}

public record EnvoyerOtpDemandeAccesRequest(string Email, string Telephone, string Prenom, string Canal);
public record VerifierOtpDemandeAccesRequest(Guid VerificationId, string Canal, string Code);
public record OtpDemandeAccesEnvoyeDto(Guid VerificationId, DateTime ExpireLe, bool SmsEnvoye);
public record OtpDemandeAccesVerifieDto(bool EmailVerifie, bool SmsVerifie, string? VerificationToken);

public record DemandeAccesConfirmationDto(
    string Reference,
    string Message,
    bool SmsEnvoye = true
);

public record DemandeAccesDto(
    Guid EntrepriseId,
    string RaisonSociale,
    string MatriculeFiscal,
    string Email,
    string? Telephone,
    string Adresse,
    string Gouvernorat,
    string CodePostal,
    string? SiteWeb,
    string DevisePrincipale,
    string RespPrenom,
    string RespNom,
    string RespEmail,
    string? RespTelephone,
    string? RespFonction,
    DateTime DateDemande,
    string Statut,
    int ScoreKyc,
    string? DonneesScoring,
    string? DonneesInscription,
    string? DocumentsUploades
);

public record StatutCompteDto(string Statut);
