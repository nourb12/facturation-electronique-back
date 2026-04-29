





namespace Einvoicing.Domain.Entities;





public sealed record ClaimsResult(
    Guid UtilisateurId,
    string JwtId,
    string Role
);