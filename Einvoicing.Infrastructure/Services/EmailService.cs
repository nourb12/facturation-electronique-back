using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Einvoicing.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Einvoicing.Infrastructure.Services;

public sealed class EmailService(
    IConfiguration config,
    ILogger<EmailService> logger
) : IEmailService
{
    private string Host => config["Email:Smtp"] ?? "smtp.gmail.com";
    private int Port => config.GetValue<int?>("Email:Port") ?? 587;
    private string Username => config["Email:Utilisateur"] ?? string.Empty;
    private string Password => config["Email:MotDePasse"] ?? string.Empty;
    private string From => config["Email:From"] ?? config["Email:Utilisateur"] ?? string.Empty;
    private string FromName => config["Email:NomExpediteur"] ?? "TunisFlow";
    private string FrontendBaseUrl => (config["Frontend:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
    private string ApiBaseUrl => (config["App:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    private bool EstModeDev =>
        string.IsNullOrWhiteSpace(Username) ||
        string.IsNullOrWhiteSpace(Password) ||
        Password == "DEMO";

    public Task EnvoyerOtpAsync(string email, string prenom, string otp, CancellationToken ct = default)
    {
        var sujet = "Votre code de réinitialisation - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:500px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
              </div>
              <h2 style="color:#111;font-size:20px">Réinitialisation de mot de passe</h2>
              <p style="color:#555">Bonjour <strong>{prenom}</strong>,</p>
              <p style="color:#555">Votre code de vérification est :</p>
              <div style="font-size:38px;font-weight:bold;letter-spacing:10px;background:#fffbe8;border:2px solid #FFE600;padding:20px;text-align:center;border-radius:10px;margin:20px 0;color:#0A0A0A">{otp}</div>
              <p style="color:#888;font-size:13px">Ce code expire dans <strong>10 minutes</strong>.</p>
              <p style="color:#888;font-size:13px">Si vous n'avez pas fait cette demande, ignorez cet email.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerAlerteConnexionAsync(string email, string prenom, string ip, string appareil, CancellationToken ct = default)
    {
        var sujet = "Nouvelle connexion détectée - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:500px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
              </div>
              <h2 style="color:#F59E0B">Nouvelle connexion</h2>
              <p style="color:#555">Bonjour <strong>{prenom}</strong>,</p>
              <p style="color:#555">Une nouvelle connexion a été détectée sur votre compte :</p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0;background:#f8fafc;border-radius:8px;overflow:hidden">
                <tr><td style="padding:10px 14px;font-weight:bold;color:#555;width:120px">Adresse IP</td><td style="padding:10px 14px;color:#111">{ip}</td></tr>
                <tr style="background:#fff"><td style="padding:10px 14px;font-weight:bold;color:#555">Appareil</td><td style="padding:10px 14px;color:#111">{appareil}</td></tr>
                <tr><td style="padding:10px 14px;font-weight:bold;color:#555">Date</td><td style="padding:10px 14px;color:#111">{DateTime.Now:dd/MM/yyyy HH:mm}</td></tr>
              </table>
              <p style="color:#EF4444;font-size:13px">Si ce n'est pas vous, changez votre mot de passe immédiatement.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerConfirmationReinitialisationAsync(string email, string prenom, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Mot de passe modifié - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:500px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
              </div>
              <h2 style="color:#22C55E">Mot de passe modifié</h2>
              <p style="color:#555">Bonjour <strong>{prenom}</strong>,</p>
              <p style="color:#555">Votre mot de passe a été réinitialisé avec succès.</p>
              <p style="color:#555">Toutes vos sessions actives ont été révoquées par mesure de sécurité.</p>
              <a href="{loginUrl}" style="display:inline-block;margin-top:16px;padding:12px 24px;background:#FFE600;color:#000;font-weight:700;border-radius:8px;text-decoration:none">Se connecter</a>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerBienvenueAsync(string email, string prenom, CancellationToken ct = default)
    {
        var dashboardUrl = $"{FrontendBaseUrl}/dashboard";
        var sujet = "Bienvenue sur TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:500px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
              </div>
              <h2 style="color:#111">Bienvenue, {prenom} !</h2>
              <p style="color:#555">Votre compte TunisFlow a été créé avec succès.</p>
              <p style="color:#555">Vous pouvez dès maintenant émettre des factures électroniques conformes DGI Tunisie.</p>
              <a href="{dashboardUrl}" style="display:inline-block;margin-top:16px;padding:12px 24px;background:#FFE600;color:#000;font-weight:700;border-radius:8px;text-decoration:none">Accéder au dashboard</a>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerBienvenueEntrepriseAsync(string email, string prenom, string nom, string nomEntreprise, string matriculeFiscal, string role, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Bienvenue sur TunisFlow - Votre compte est activé";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:20px 28px;border-radius:12px;margin-bottom:28px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
                <div style="color:#666;font-size:11px;margin-top:4px">Facturation électronique certifiée · TEIF 2026</div>
              </div>
              <h2 style="color:#111;font-size:22px;margin-bottom:8px">Bienvenue, {prenom} {nom} !</h2>
              <p style="color:#555;margin-bottom:20px">Votre espace TunisFlow est maintenant activé et conforme TEIF 2026.</p>
              <div style="background:#fffbe8;border:1px solid #FFE600;border-radius:10px;padding:16px 20px;margin-bottom:20px">
                <table style="width:100%;border-collapse:collapse">
                  <tr><td style="padding:6px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase;width:150px">Entreprise</td><td style="padding:6px 0;font-size:14px;color:#111;font-weight:600">{nomEntreprise}</td></tr>
                  <tr><td style="padding:6px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase">Matricule fiscal</td><td style="padding:6px 0;font-size:14px;color:#111;font-family:monospace">{matriculeFiscal}</td></tr>
                  <tr><td style="padding:6px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase">Email</td><td style="padding:6px 0;font-size:14px;color:#111">{email}</td></tr>
                  <tr><td style="padding:6px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase">Rôle</td><td style="padding:6px 0;font-size:14px;color:#111">{role}</td></tr>
                </table>
              </div>
              <a href="{loginUrl}" style="display:inline-block;padding:13px 28px;background:#0A0A0A;color:#FFE600;font-weight:700;border-radius:10px;text-decoration:none;font-size:14px">Accéder à mon tableau de bord</a>
              <hr style="border:none;border-top:1px solid #eee;margin:28px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · UBL 2.1 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerNotificationNouvelleDemandeAsync(string adminEmail, string raisonSociale, string matriculeFiscal, string emailEntreprise, string telephone, Guid entrepriseId, List<string> documents, CancellationToken ct = default)
    {
        var docsList = string.Join(string.Empty, documents.Select(d => $"<li style='padding:4px 0;color:#555'>✅ {d}</li>"));
        var validerUrl = $"{ApiBaseUrl}/api/auth/demandes-acces/{entrepriseId}/valider";
        var rejeterUrl = $"{FrontendBaseUrl}/admin/entreprises?filter=EnAttente";

        var sujet = "Nouvelle demande d'accès - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px;display:flex;align-items:center;gap:12px">
                <div style="background:#FFE600;padding:6px 12px;border-radius:6px;font-size:14px;font-weight:900;color:#000">TF</div>
                <span style="color:#fff;font-size:14px">TunisFlow - Nouvelle demande d'accès</span>
              </div>
              <div style="background:#FFF8E7;border:1px solid #FFE600;border-radius:10px;padding:16px 20px;margin-bottom:20px">
                <p style="margin:0;font-size:14px;font-weight:600;color:#92400E">🔔 Une nouvelle demande d'accès a été soumise</p>
              </div>
              <table style="width:100%;border-collapse:collapse;margin-bottom:20px">
                <tr style="background:#F9FAFB"><td style="padding:10px 14px;font-size:13px;font-weight:600;color:#374151;border:1px solid #E5E7EB;width:40%">Raison sociale</td><td style="padding:10px 14px;font-size:13px;color:#111827;border:1px solid #E5E7EB">{raisonSociale}</td></tr>
                <tr><td style="padding:10px 14px;font-size:13px;font-weight:600;color:#374151;border:1px solid #E5E7EB">Matricule fiscal</td><td style="padding:10px 14px;font-size:13px;color:#111827;border:1px solid #E5E7EB;font-family:monospace">{matriculeFiscal}</td></tr>
                <tr style="background:#F9FAFB"><td style="padding:10px 14px;font-size:13px;font-weight:600;color:#374151;border:1px solid #E5E7EB">Email</td><td style="padding:10px 14px;font-size:13px;color:#111827;border:1px solid #E5E7EB">{emailEntreprise}</td></tr>
                <tr><td style="padding:10px 14px;font-size:13px;font-weight:600;color:#374151;border:1px solid #E5E7EB">Téléphone</td><td style="padding:10px 14px;font-size:13px;color:#111827;border:1px solid #E5E7EB">{telephone}</td></tr>
                <tr style="background:#F9FAFB"><td style="padding:10px 14px;font-size:13px;font-weight:600;color:#374151;border:1px solid #E5E7EB">Documents reçus</td><td style="padding:10px 14px;border:1px solid #E5E7EB"><ul style="margin:0;padding-left:16px">{docsList}</ul></td></tr>
              </table>
              <div style="display:flex;gap:12px;margin-bottom:24px">
                <a href="{validerUrl}" style="flex:1;display:block;padding:13px;background:#22C55E;color:#fff;border-radius:8px;text-decoration:none;font-size:14px;font-weight:600;text-align:center">✅ Valider la demande</a>
                <a href="{rejeterUrl}" style="flex:1;display:block;padding:13px;background:#EF4444;color:#fff;border-radius:8px;text-decoration:none;font-size:14px;font-weight:600;text-align:center">❌ Consulter et rejeter</a>
              </div>
              <p style="font-size:11px;color:#9CA3AF;text-align:center">TunisFlow · Système de gestion des accès entreprise</p>
            </div>
            """;
        return EnvoyerSmtpAsync(adminEmail, sujet, corps, ct);
    }

    public Task EnvoyerConfirmationDemandeAsync(string email, string raisonSociale, string reference, string matriculeFiscal, string respPrenom, string respNom, List<string> documentsRecus, CancellationToken ct = default)
    {
        var sujet = "Demande reçue - TunisFlow";
        var docsHtml = documentsRecus.Count > 0
            ? string.Join(string.Empty, documentsRecus.Select(d => $"""
                <tr>
                  <td style="padding:7px 12px;font-size:12.5px;color:#374151;border-bottom:1px solid #F3F4F6;">
                    <span style="display:inline-block;width:16px;height:16px;border-radius:50%;background:#EAF3DE;text-align:center;line-height:16px;font-size:10px;margin-right:6px;vertical-align:middle;color:#3B6D11">✓</span>
                    {d}
                  </td>
                </tr>
              """))
            : "<tr><td style=\"padding:7px 12px;font-size:12px;color:#9CA3AF\">Aucun document soumis</td></tr>";

        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto;background:#ffffff">
              <div style="background:#0A0A0A;padding:18px 24px;border-radius:12px 12px 0 0">
                <div style="display:flex;align-items:center;gap:10px">
                  <div style="background:#FFE600;padding:5px 10px;border-radius:6px;font-size:13px;font-weight:900;color:#000;letter-spacing:-.02em">TF</div>
                  <span style="color:#fff;font-size:13px">Facturation electronique certifiee</span>
                </div>
              </div>
              <div style="padding:28px 28px 0">
                <div style="text-align:center;margin-bottom:24px">
                  <div style="width:56px;height:56px;border-radius:50%;background:rgba(34,197,94,.1);border:1.5px solid rgba(34,197,94,.3);display:inline-flex;align-items:center;justify-content:center;margin-bottom:14px">
                    <svg width="26" height="26" viewBox="0 0 26 26" fill="none">
                      <path d="M5 13l5 5.5 11-11" stroke="#22C55E" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                    </svg>
                  </div>
                  <h2 style="font-size:22px;font-weight:700;color:#111827;margin:0 0 4px">Demande reçue !</h2>
                  <p style="font-size:14px;color:#6B7280;margin:0">Bonjour <strong style="color:#111827">{raisonSociale}</strong>, votre dossier est en cours d'examen.</p>
                </div>
                <div style="display:flex;align-items:center;margin-bottom:24px;border:1px solid #E5E7EB;border-radius:10px;overflow:hidden">
                  <div style="flex:1;padding:12px 8px;text-align:center;background:#EAF3DE;border-right:1px solid #E5E7EB"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#3B6D11;margin-bottom:4px">Soumis</div><svg width="16" height="16" viewBox="0 0 16 16" fill="none" style="display:block;margin:0 auto"><circle cx="8" cy="8" r="7" fill="rgba(34,197,94,.2)" stroke="rgba(34,197,94,.4)" stroke-width="1"/><path d="M5 8l2 2 4-4" stroke="#22C55E" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg></div>
                  <div style="flex:1;padding:12px 8px;text-align:center;background:#FAEEDA;border-right:1px solid #E5E7EB"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#854F0B;margin-bottom:4px">Examen</div><svg width="16" height="16" viewBox="0 0 16 16" fill="none" style="display:block;margin:0 auto"><circle cx="8" cy="8" r="7" fill="rgba(245,158,11,.15)" stroke="rgba(245,158,11,.4)" stroke-width="1"/><circle cx="8" cy="8" r="3" fill="#F59E0B"/></svg></div>
                  <div style="flex:1;padding:12px 8px;text-align:center;background:#F9FAFB"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#9CA3AF;margin-bottom:4px">Accès</div><svg width="16" height="16" viewBox="0 0 16 16" fill="none" style="display:block;margin:0 auto"><circle cx="8" cy="8" r="7" stroke="#E5E7EB" stroke-width="1.5" fill="none"/></svg></div>
                </div>
                <div style="background:#0D1117;border-radius:10px;padding:16px 18px;margin-bottom:20px">
                  <div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#6B7280;margin-bottom:8px">Référence de votre demande</div>
                  <div style="font-size:17px;font-weight:700;font-family:monospace;letter-spacing:.08em;color:#FFE600">{reference}</div>
                  <div style="font-size:10.5px;color:#4B5563;margin-top:4px">Conservez cette référence pour tout suivi de dossier.</div>
                </div>
                <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:20px">
                  <div style="background:#F9FAFB;border-radius:8px;padding:10px 12px;border:1px solid #F3F4F6"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#9CA3AF;margin-bottom:3px">Entreprise</div><div style="font-size:12px;font-weight:600;color:#111827">{raisonSociale}</div></div>
                  <div style="background:#F9FAFB;border-radius:8px;padding:10px 12px;border:1px solid #F3F4F6"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#9CA3AF;margin-bottom:3px">Matricule fiscal</div><div style="font-size:12px;font-weight:600;color:#111827;font-family:monospace">{matriculeFiscal}</div></div>
                  <div style="background:#F9FAFB;border-radius:8px;padding:10px 12px;border:1px solid #F3F4F6"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#9CA3AF;margin-bottom:3px">Responsable</div><div style="font-size:12px;font-weight:600;color:#111827">{respPrenom} {respNom}</div></div>
                  <div style="background:#FAEEDA;border-radius:8px;padding:10px 12px;border:1px solid #FCD34D"><div style="font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#854F0B;margin-bottom:3px">Délai estimé</div><div style="font-size:12px;font-weight:700;color:#92400E">48h ouvrables</div></div>
                </div>
                <div style="border:1px solid #E5E7EB;border-radius:8px;overflow:hidden;margin-bottom:20px">
                  <div style="background:#F9FAFB;padding:9px 12px;border-bottom:1px solid #E5E7EB"><span style="font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#6B7280">Documents reçus</span></div>
                  <table style="width:100%;border-collapse:collapse">{docsHtml}</table>
                </div>
                <a href="mailto:support@tunisflow.tn" style="display:block;padding:13px;background:#FFE600;color:#0A0A0A;border-radius:8px;text-decoration:none;font-size:14px;font-weight:700;text-align:center;margin-bottom:20px">Contacter le support -></a>
              </div>
              <div style="text-align:center;padding:16px 24px 20px;border-top:1px solid #F3F4F6;margin-top:4px">
                <p style="font-size:10.5px;color:#9CA3AF;margin:0;line-height:1.7">TunisFlow · Facturation electronique certifiee<br>Pour toute question : <a href="mailto:support@tunisflow.tn" style="color:#854F0B;text-decoration:none">support@tunisflow.tn</a><br><span style="color:#D1D5DB">Conforme TEIF 2026 · Données chiffrées AES-256 · UBL 2.1</span></p>
              </div>
            </div>
            """;

        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerAccesValideAsync(string email, string prenom, string raisonSociale, string loginEmail, string motDePasseTemp, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Accès validé - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:540px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:28px">
                <span style="background:#FFE600;padding:5px 10px;border-radius:5px;font-size:13px;font-weight:900;color:#000">TF</span>
                <span style="color:#fff;font-size:13px;margin-left:10px">Facturation electronique certifiee</span>
              </div>
              <div style="background:#F0FDF4;border:1px solid rgba(34,197,94,.3);border-radius:10px;padding:16px 20px;margin-bottom:24px">
                <p style="font-size:15px;font-weight:700;color:#065F46;margin:0 0 4px">🎉 Votre accès est confirmé !</p>
                <p style="font-size:13px;color:#374151;margin:0">Bonjour <strong>{prenom}</strong>, votre compte <strong>{raisonSociale}</strong> est maintenant actif.</p>
              </div>
              <p style="font-size:14px;color:#374151;margin:0 0 20px">Voici vos identifiants de connexion à la plateforme TunisFlow :</p>
              <div style="background:#1A1A2E;border-radius:10px;padding:20px;margin-bottom:20px">
                <div style="margin-bottom:16px"><p style="font-size:11px;color:#9CA3AF;margin:0 0 4px;text-transform:uppercase;letter-spacing:.06em">Identifiant (email)</p><p style="font-size:15px;font-weight:600;color:#E8EDF5;font-family:monospace;margin:0">{loginEmail}</p></div>
                <div><p style="font-size:11px;color:#9CA3AF;margin:0 0 4px;text-transform:uppercase;letter-spacing:.06em">Mot de passe temporaire</p><p style="font-size:18px;font-weight:700;color:#FFE600;font-family:monospace;letter-spacing:.12em;margin:0">{motDePasseTemp}</p></div>
              </div>
              <div style="background:#FEF3C7;border-radius:8px;padding:12px 14px;margin-bottom:20px"><p style="font-size:12px;color:#92400E;margin:0">⚠ Ce mot de passe est temporaire. Vous serez invité à le modifier lors de votre première connexion.</p></div>
              <a href="{loginUrl}" style="display:block;padding:14px;background:#FFE600;color:#0A0A0A;border-radius:8px;text-decoration:none;font-size:15px;font-weight:700;text-align:center;margin-bottom:24px">Accéder à la plateforme -></a>
              <p style="font-size:11px;color:#9CA3AF;text-align:center">Si vous n'êtes pas à l'origine de cette demande, contactez immédiatement support@tunisflow.tn</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerDemandeRejeteeAsync(string email, string prenom, string raisonSociale, string motif, CancellationToken ct = default)
    {
        var sujet = "Demande rejetée - TunisFlow";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:540px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:28px">
                <span style="background:#FFE600;padding:5px 10px;border-radius:5px;font-size:13px;font-weight:900;color:#000">TF</span>
                <span style="color:#fff;font-size:13px;margin-left:10px">Facturation electronique certifiee</span>
              </div>
              <p style="font-size:14px;color:#374151;margin:0 0 12px">Bonjour <strong>{prenom}</strong>,</p>
              <p style="font-size:14px;color:#374151;margin:0 0 20px">Après examen de votre dossier, nous ne sommes pas en mesure d'activer le compte de <strong>{raisonSociale}</strong> pour le motif suivant :</p>
              <div style="background:#FEF2F2;border:1px solid rgba(239,68,68,.3);border-radius:8px;padding:14px 16px;margin-bottom:20px"><p style="font-size:13px;color:#991B1B;margin:0">{motif}</p></div>
              <p style="font-size:13px;color:#6B7280;margin:0 0 8px">Vous pouvez soumettre une nouvelle demande en corrigeant les points mentionnés ci-dessus, ou contacter notre équipe pour plus d'informations.</p>
              <p style="font-size:13px;color:#6B7280;text-align:center;margin-top:24px">Contact : <a href="mailto:support@tunisflow.tn" style="color:#92400E">support@tunisflow.tn</a></p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    public Task EnvoyerNotifFactureAsync(string email, string nomClient, string sujet, string numeroFacture, string type, decimal montantTtc, CancellationToken ct = default)
    {
        var (couleur, statutLabel) = type switch
        {
            "validee" => ("#3B82F6", "Validée"),
            "rejetee" => ("#EF4444", "Rejetée"),
            "payee" => ("#22C55E", "Payée"),
            "rappel" => ("#F59E0B", "Échéance proche"),
            _ => ("#555555", type)
        };

        var facturesUrl = $"{FrontendBaseUrl}/factures";
        var corps = $"""
            <div style="font-family:Arial,sans-serif;max-width:500px;margin:auto;padding:24px">
              <div style="background:#0A0A0A;padding:16px 24px;border-radius:10px;margin-bottom:24px">
                <span style="color:#FFE600;font-size:18px;font-weight:900">TF</span>
                <span style="color:#fff;font-size:14px;margin-left:10px">TunisFlow</span>
              </div>
              <h2 style="color:{couleur}">{sujet}</h2>
              <p style="color:#555">Bonjour <strong>{nomClient}</strong>,</p>
              <div style="background:#f8fafc;border-left:4px solid {couleur};border-radius:0 8px 8px 0;padding:16px 20px;margin:20px 0">
                <table style="width:100%;border-collapse:collapse">
                  <tr><td style="padding:5px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase;width:140px">N° Facture</td><td style="padding:5px 0;font-size:14px;color:#111;font-family:monospace;font-weight:700">{numeroFacture}</td></tr>
                  <tr><td style="padding:5px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase">Statut</td><td style="padding:5px 0"><span style="background:{couleur}22;color:{couleur};padding:2px 10px;border-radius:99px;font-size:12px;font-weight:700">{statutLabel}</span></td></tr>
                  <tr><td style="padding:5px 0;font-size:12px;color:#888;font-weight:700;text-transform:uppercase">Montant TTC</td><td style="padding:5px 0;font-size:16px;color:#111;font-weight:700;font-family:monospace">{montantTtc:N3} TND</td></tr>
                </table>
              </div>
              <a href="{facturesUrl}" style="display:inline-block;padding:12px 24px;background:#0A0A0A;color:#FFE600;font-weight:700;border-radius:8px;text-decoration:none;font-size:13px">Voir la facture</a>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#bbb;font-size:11px;text-align:center">TunisFlow · Conforme TEIF 2026 · DGI Tunisie</p>
            </div>
            """;
        return EnvoyerSmtpAsync(email, sujet, corps, ct);
    }

    private async Task EnvoyerSmtpAsync(string destinataire, string sujet, string corpsHtml, CancellationToken ct = default)
    {
        if (EstModeDev)
        {
            logger.LogInformation("[EMAIL DEV] -> {Dest} | Sujet: {Sujet}", destinataire, sujet);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(FromName, From));
            message.To.Add(MailboxAddress.Parse(destinataire));
            message.Subject = sujet;
            message.Body = new TextPart("html") { Text = corpsHtml };

            using var client = new SmtpClient();
            await client.ConnectAsync(Host, Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(Username, Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("Email envoye -> {Dest} [{Sujet}]", destinataire, sujet);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Echec envoi email -> {Dest}", destinataire);
        }
    }
}
