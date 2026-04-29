




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;

public sealed class UtilisateurService(
    IUtilisateurRepository utilisateurRepo,
    ISessionRepository sessionRepo,
    IPasswordHasher passwordHasher
) : IUtilisateurService
{
    public async Task<UtilisateurListeDto> CreerAsync(
        Guid entrepriseId, CreerUtilisateurRequest req, CancellationToken ct = default)
    {
        var existant = await utilisateurRepo.ObtenirParEmailAsync(req.Email.ToLower(), ct);
        if (existant is not null)
            throw new ConflitException("Cet email est déjà utilisé.");

        if (req.MotDePasse != req.ConfirmationMotDePasse)
            throw new ValidationMetierException("Les mots de passe ne correspondent pas.");

        var hash = passwordHasher.Hacher(req.MotDePasse);
        var utilisateur = Utilisateur.Creer(
            req.Prenom, req.Nom, req.Email.ToLower(),
            hash, req.Role, entrepriseId);

        await utilisateurRepo.AjouterAsync(utilisateur, ct);
        await utilisateurRepo.SauvegarderAsync(ct);
        return MapToDto(utilisateur);
    }

    public async Task<IReadOnlyList<UtilisateurListeDto>> ListerAsync(
        Guid entrepriseId, CancellationToken ct = default)
    {
        var liste = await utilisateurRepo.ListerParEntrepriseAsync(entrepriseId, ct);
        return liste.Select(MapToDto).ToList();
    }

    public async Task<UtilisateurListeDto> ObtenirParIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        return MapToDto(u);
    }

    public async Task<UtilisateurListeDto> MettreAJourAsync(
        Guid id, MettreAJourUtilisateurRequest req, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        u.MettreAJourProfil(req.Prenom, req.Nom, req.Telephone, req.Poste, req.Departement);
        utilisateurRepo.MettreAJour(u);
        await utilisateurRepo.SauvegarderAsync(ct);
        return MapToDto(u);
    }

    public async Task SuspendreAsync(Guid id, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        u.Suspendre();
        utilisateurRepo.MettreAJour(u);
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    public async Task ReactiverAsync(Guid id, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        u.Reactiver();
        utilisateurRepo.MettreAJour(u);
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    public async Task SupprimerAsync(Guid id, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");
        u.Supprimer();
        utilisateurRepo.MettreAJour(u);
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    public async Task ChangerMotDePasseAsync(
        Guid id, ChangerMotDePasseRequest req, CancellationToken ct = default)
    {
        var u = await utilisateurRepo.ObtenirParIdAsync(id, ct)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (!passwordHasher.Verifier(req.MotDePasseActuel, u.MotDePasseHash))
            throw new ValidationMetierException("Le mot de passe actuel est incorrect.");

        if (req.NouveauMotDePasse != req.ConfirmationMotDePasse)
            throw new ValidationMetierException("Les nouveaux mots de passe ne correspondent pas.");

        if (req.NouveauMotDePasse.Length < 8)
            throw new ValidationMetierException("Le mot de passe doit contenir au moins 8 caractères.");

        var hash = passwordHasher.Hacher(req.NouveauMotDePasse);
        u.ChangerMotDePasse(hash);
        utilisateurRepo.MettreAJour(u);
        await utilisateurRepo.SauvegarderAsync(ct);
    }

    public async Task<IReadOnlyList<SessionActiveDto>> ObtenirSessionsAsync(
        Guid utilisateurId, CancellationToken ct = default)
    {
        var sessions = await sessionRepo.ListerAsync(utilisateurId, ct);
        return sessions.Select(s => new SessionActiveDto(
            s.Id,
            s.Appareil ?? "Appareil inconnu",
            s.AdresseIp ?? "—",
            s.UserAgent ?? "—",
            s.CreeLe,
            s.DerniereActivite,
            false
        )).ToList();
    }

    public async Task RevoquerSessionAsync(
        Guid utilisateurId, Guid sessionId, CancellationToken ct = default)
    {
        await sessionRepo.SupprimerAsync(sessionId, ct);
        await sessionRepo.SauvegarderAsync(ct);
    }

    public async Task RevoquerToutesSessionsAsync(
        Guid utilisateurId, CancellationToken ct = default)
    {
        var sessions = await sessionRepo.ListerAsync(utilisateurId, ct);
        foreach (var s in sessions)
            await sessionRepo.SupprimerAsync(s.Id, ct);
        await sessionRepo.SauvegarderAsync(ct);
    }

    private static UtilisateurListeDto MapToDto(Utilisateur u) => new(
        u.Id, u.Prenom, u.Nom, u.Email,
        u.Role.ToString(), u.Statut.ToString(),
        u.DeuxFAActif, u.Telephone, u.Poste, u.Departement,
        u.AlerteConnexion, u.CreeLe, u.DerniereConnexion);
}
