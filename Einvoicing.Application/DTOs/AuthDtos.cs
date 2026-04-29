




namespace Einvoicing.Application.DTOs;







public record RegisterRequest(
    string Prenom,
    string Nom,
    string Email,
    string MotDePasse,
    string ConfirmationMotDePasse,
    string NomEntreprise,
    string MatriculeFiscal,
    string Adresse,
    string Ville,
    string CodePostal,
    string Telephone,
    string? SiteWeb,
    string DevisePrincipale
);





public record LoginRequest(
    string Email,
    string MotDePasse
);





public record Login2FARequest(
    Guid UtilisateurId,
    string Code
);





public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);





public record LogoutRequest(
    string RefreshToken
);





public record MotDePasseOublieRequest(
    string Courriel
);





public record VerifierOtpRequest(
    string Courriel,
    string Otp
);





public record ReinitialiserMotDePasseRequest(
    string Courriel,
    string Otp,
    string NouveauMotDePasse,
    string ConfirmationMotDePasse
);





public record Activer2FARequest(
    string Secret,
    string Code
);






public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpireA,
    UtilisateurDto Utilisateur
);




public record DeuxFARequisResponse(
    Guid UtilisateurId
);




public record Activation2FAResponse(
    string Secret,
    string QrCodeUri
);




public record SuccesResponse(
    string Message
);




public record UtilisateurDto(
    Guid Id,
    string Prenom,
    string Nom,
    string Email,
    string Role,
    string Statut,
    bool DeuxFAActif,
    Guid? EntrepriseId,
    DateTime? DerniereConnexion,
    string? Telephone,
    string? Poste,
    string? Departement,
    bool AlerteConnexion
);
