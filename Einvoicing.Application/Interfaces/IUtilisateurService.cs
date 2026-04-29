using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IUtilisateurService
{
    Task<UtilisateurListeDto> CreerAsync(Guid entrepriseId, CreerUtilisateurRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UtilisateurListeDto>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<UtilisateurListeDto> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<UtilisateurListeDto> MettreAJourAsync(Guid id, MettreAJourUtilisateurRequest request, CancellationToken ct = default);
    Task SuspendreAsync(Guid id, CancellationToken ct = default);
    Task ReactiverAsync(Guid id, CancellationToken ct = default);
    Task SupprimerAsync(Guid id, CancellationToken ct = default);
    Task ChangerMotDePasseAsync(Guid id, ChangerMotDePasseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SessionActiveDto>> ObtenirSessionsAsync(Guid utilisateurId, CancellationToken ct = default);
    Task RevoquerSessionAsync(Guid utilisateurId, Guid sessionId, CancellationToken ct = default);
    Task RevoquerToutesSessionsAsync(Guid utilisateurId, CancellationToken ct = default);
}
