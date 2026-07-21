using Einvoicing.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using static System.Net.Mime.MediaTypeNames;

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
    private string FromName => config["Email:NomExpediteur"] ?? "TuniFlow";
    private string FrontendBaseUrl => (config["Frontend:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
    private string ApiBaseUrl => (config["App:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    private bool EstModeDev =>
        string.IsNullOrWhiteSpace(Username) ||
        string.IsNullOrWhiteSpace(Password) ||
        Password == "DEMO";

    // ─── Shared base layout ──────────────────────────────────────────────────

    /// <summary>
    /// Wraps any inner HTML content into the standard TuniFlow email shell.
    /// </summary>
    private static string Layout(string bodyContent, string previewText = "") => $$"""
        <!DOCTYPE html>
        <html lang="fr">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width,initial-scale=1.0">
          <meta name="x-apple-disable-message-reformatting">
          <!--[if !mso]><!-->
          <meta http-equiv="X-UA-Compatible" content="IE=edge">
          <!--<![endif]-->
          <title>TuniFlow</title>
          <style>
            body,table,td,a{-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%}
            table,td{mso-table-lspace:0pt;mso-table-rspace:0pt}
            img{-ms-interpolation-mode:bicubic;border:0;height:auto;line-height:100%;outline:none;text-decoration:none}
            body{margin:0!important;padding:0!important;background-color:#F4F4F5}
            .btn:hover{opacity:.88!important}
          </style>
        </head>
        <body style="margin:0;padding:0;background-color:#F4F4F5;font-family:Arial,Helvetica,sans-serif">
          <!-- Preview text (hidden) -->
          {{(string.IsNullOrEmpty(previewText) ? "" : $"<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all\">{previewText}&nbsp;&zwnj;&hairsp;&hairsp;&hairsp;&hairsp;&hairsp;&hairsp;&hairsp;&hairsp;</div>")}}

          <!-- Outer wrapper -->
          <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#F4F4F5;padding:32px 0">
            <tr>
              <td align="center">
                <!-- Card -->
                <table width="580" cellpadding="0" cellspacing="0" border="0" style="max-width:580px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #E4E4E7">

                  <!-- Yellow top accent bar -->
                  <tr>
                    <td height="4" style="background:#FFE600;font-size:0;line-height:0">&nbsp;</td>
                  </tr>

                  <!-- Header -->
                  <tr>
                    <td style="padding:24px 32px 20px;border-bottom:1px solid #F0F0F0">
                      <table width="100%" cellpadding="0" cellspacing="0" border="0">
                        <tr>
                          <td>
                            <!-- Logo mark -->
                            <table cellpadding="0" cellspacing="0" border="0" style="display:inline-table">
                              <tr>
                                <td style="background:#FFE600;border-radius:6px;padding:5px 11px">
                                  <span style="font-size:14px;font-weight:900;color:#0A0A0A;letter-spacing:-.02em;font-family:Arial,sans-serif">TF</span>
                                </td>
                                <td style="padding-left:10px;vertical-align:middle">
                                  <span style="font-size:15px;font-weight:700;color:#0A0A0A;font-family:Arial,sans-serif">TuniFlow</span>
                                </td>
                              </tr>
                            </table>
                          </td>
                          <td align="right" style="vertical-align:middle">
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>

                  <!-- Body content -->
                  <tr>
                    <td style="padding:32px 32px 24px">
                      {{bodyContent}}
                    </td>
                  </tr>

                  <!-- Footer -->
                  <tr>
                    <td style="background:#FAFAFA;border-top:1px solid #F0F0F0;padding:16px 32px;text-align:center">
                      <p style="font-size:11px;color:#A1A1AA;margin:0;line-height:1.7;font-family:Arial,sans-serif">
                        © 2026 TuniFlow · Ernst &amp; Young Tunisia<br>
                        Questions ? <a href="mailto:support@tuniflow.tn" style="color:#71717A;text-decoration:none">support@tuniflow.tn</a>
                        &nbsp;·&nbsp;
                        <a href="mailto:demo@tuniflow.tn" style="color:#71717A;text-decoration:none">demo@tuniflow.tn</a>
                      </p>
                    </td>
                  </tr>

                </table>
                <!-- /Card -->
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    // ─── Reusable HTML snippets ───────────────────────────────────────────────

    private static string Heading(string text) =>
        $"""<h1 style="font-size:22px;font-weight:700;color:#0A0A0A;margin:0 0 8px;font-family:Arial,sans-serif">{text}</h1>""";

    private static string SubHeading(string text) =>
        $"""<p style="font-size:14px;color:#52525B;margin:0 0 20px;line-height:1.6;font-family:Arial,sans-serif">{text}</p>""";

    private static string Paragraph(string html) =>
        $"""<p style="font-size:14px;color:#52525B;line-height:1.7;margin:0 0 16px;font-family:Arial,sans-serif">{html}</p>""";

    private static string PrimaryButton(string url, string label) => $"""
        <table cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 4px">
          <tr>
            <td style="background:#FFE600;border-radius:8px">
              <a href="{url}" class="btn" style="display:inline-block;padding:13px 28px;color:#0A0A0A;font-size:14px;font-weight:700;text-decoration:none;font-family:Arial,sans-serif;letter-spacing:-.01em">{label} →</a>
            </td>
          </tr>
        </table>
        """;

    private static string InfoBox(string innerHtml, string bgColor = "#FAFAFA", string borderColor = "#E4E4E7") => $"""
        <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{bgColor};border:1px solid {borderColor};border-radius:10px;margin:0 0 20px">
          <tr><td style="padding:16px 20px">{innerHtml}</td></tr>
        </table>
        """;

    private static string AlertBox(string innerHtml, string bgColor, string borderColor, string textColor) => $"""
        <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{bgColor};border-left:4px solid {borderColor};border-radius:0 8px 8px 0;margin:0 0 20px">
          <tr><td style="padding:12px 16px;font-size:13px;color:{textColor};font-family:Arial,sans-serif;line-height:1.6">{innerHtml}</td></tr>
        </table>
        """;

    private static string InfoRow(string label, string value) => $"""
        <tr>
          <td style="padding:8px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.05em;width:160px;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;vertical-align:top">{label}</td>
          <td style="padding:8px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5">{value}</td>
        </tr>
        """;

    private static string Divider() =>
        """<table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:20px 0"><tr><td height="1" style="background:#F0F0F0;font-size:0;line-height:0">&nbsp;</td></tr></table>""";

    // ─── Email methods ────────────────────────────────────────────────────────

    public Task SendEmailAsync(string destinataire, string sujet, string corpsHtml, CancellationToken ct = default)
        => EnvoyerSmtpAsync(destinataire, sujet, corpsHtml, ct);

    // OTP / Réinitialisation
    public Task EnvoyerOtpAsync(string email, string prenom, string otp, CancellationToken ct = default)
    {
        var sujet = "Votre code de réinitialisation — TuniFlow";
        var body = $"""
            {Heading("Réinitialisation de mot de passe")}
            {SubHeading($"Bonjour <strong>{prenom}</strong>, voici votre code de vérification :")}

            <!-- OTP block -->
            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px">
              <tr>
                <td align="center" style="background:#FFFBEB;border:2px solid #FFE600;border-radius:12px;padding:28px 20px">
                  <span style="font-size:42px;font-weight:900;letter-spacing:14px;color:#0A0A0A;font-family:'Courier New',monospace">{otp}</span>
                  <p style="font-size:12px;color:#71717A;margin:12px 0 0;font-family:Arial,sans-serif">Expire dans <strong>10 minutes</strong></p>
                </td>
              </tr>
            </table>

            {AlertBox("Si vous n'avez pas fait cette demande, ignorez cet e-mail. Votre mot de passe reste inchangé.", "#FEF2F2", "#EF4444", "#991B1B")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body, $"Votre code : {otp}"), ct);
    }

    // Alerte connexion
    public Task EnvoyerAlerteConnexionAsync(string email, string prenom, string ip, string appareil, CancellationToken ct = default)
    {
        var sujet = "Nouvelle connexion détectée — TuniFlow";
        var body = $"""
            {Heading("Nouvelle connexion détectée")}
            {Paragraph($"Bonjour <strong>{prenom}</strong>, nous avons détecté une connexion à votre compte TuniFlow.")}

            {InfoBox($"""
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {InfoRow("Adresse IP", ip)}
                {InfoRow("Appareil", appareil)}
                {InfoRow("Date &amp; heure", DateTime.Now.ToString("dd/MM/yyyy à HH:mm"))}
              </table>
            """, "#FAFAFA", "#E4E4E7")}

            {AlertBox("<strong>Ce n'était pas vous ?</strong> Changez votre mot de passe immédiatement et contactez notre support.", "#FEF2F2", "#EF4444", "#991B1B")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body), ct);
    }

    // Confirmation réinitialisation
    public Task EnvoyerConfirmationReinitialisationAsync(string email, string prenom, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Mot de passe modifié — TuniFlow";
        var body = $"""
            {Heading("Mot de passe mis à jour ✓")}
            {Paragraph($"Bonjour <strong>{prenom}</strong>, votre mot de passe a été réinitialisé avec succès.")}
            {Paragraph("Par mesure de sécurité, toutes vos sessions actives ont été révoquées. Reconnectez-vous avec votre nouveau mot de passe.")}
            {PrimaryButton(loginUrl, "Se connecter")}
            {Divider()}
            {AlertBox("Si vous n'êtes pas à l'origine de cette action, contactez-nous immédiatement à <a href=\"mailto:support@tuniflow.tn\" style=\"color:#991B1B\">support@tuniflow.tn</a>.", "#FEF2F2", "#EF4444", "#991B1B")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body), ct);
    }

    // Bienvenue (compte simple)
    public Task EnvoyerBienvenueAsync(string email, string prenom, CancellationToken ct = default)
    {
        var dashboardUrl = $"{FrontendBaseUrl}/dashboard";
        var sujet = "Bienvenue sur TuniFlow";
        var body = $"""
            {Heading($"Bienvenue, {prenom} !")}
            {Paragraph("Votre compte TuniFlow a été créé avec succès. Vous pouvez dès maintenant émettre des factures électroniques conformes DGI Tunisie.")}
            {PrimaryButton(dashboardUrl, "Accéder au tableau de bord")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body, $"Bienvenue {prenom} — votre compte est prêt."), ct);
    }

    // Bienvenue entreprise (compte activé avec identifiants complets)
    public Task EnvoyerBienvenueEntrepriseAsync(string email, string prenom, string nom, string nomEntreprise, string matriculeFiscal, string role, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Bienvenue sur TuniFlow — Votre compte est activé";
        var body = $"""
            {Heading($"Bienvenue, {prenom} {nom} !")}
            {Paragraph("Votre espace TuniFlow est maintenant activé et conforme TEIF 2026. Retrouvez ci-dessous les informations de votre compte.")}

            {InfoBox($"""
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {InfoRow("Entreprise", nomEntreprise)}
                {InfoRow("Matricule fiscal", $"<span style=\"font-family:'Courier New',monospace\">{matriculeFiscal}</span>")}
                {InfoRow("Email", email)}
                {InfoRow("Rôle", role)}
              </table>
            """, "#FAFAFA", "#E4E4E7")}

            {PrimaryButton(loginUrl, "Accéder à mon tableau de bord")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body, $"Votre compte {nomEntreprise} est actif."), ct);
    }

    // Notification nouvelle demande (admin)
    public Task EnvoyerNotificationNouvelleDemandeAsync(string adminEmail, string raisonSociale, string matriculeFiscal, string emailEntreprise, string telephone, Guid entrepriseId, List<string> documents, CancellationToken ct = default)
    {
        var validerUrl = $"{ApiBaseUrl}/api/auth/demandes-acces/{entrepriseId}/valider";
        var rejeterUrl = $"{FrontendBaseUrl}/admin/entreprises?filter=EnAttente";
        var docsList = string.Join("", documents.Select(d =>
            $"<tr><td style=\"padding:7px 12px 7px 0;font-size:13px;color:#52525B;border-bottom:1px solid #F4F4F5;font-family:Arial,sans-serif\">✓ &nbsp;{d}</td></tr>"));

        var sujet = $"[Nouvelle demande] {raisonSociale} — TuniFlow";
        var body = $"""
            {AlertBox($"🔔 &nbsp;<strong>Nouvelle demande d'accès soumise</strong> — une action est requise.", "#FFFBEB", "#FFE600", "#78350F")}

            {Heading("Détails de la demande")}

            {InfoBox($"""
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {InfoRow("Raison sociale", raisonSociale)}
                {InfoRow("Matricule fiscal", $"<span style=\"font-family:'Courier New',monospace\">{matriculeFiscal}</span>")}
                {InfoRow("Email", emailEntreprise)}
                {InfoRow("Téléphone", telephone)}
              </table>
            """, "#FAFAFA", "#E4E4E7")}

            <!-- Documents -->
            <p style="font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#A1A1AA;margin:0 0 8px;font-family:Arial,sans-serif">Documents reçus</p>
            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px">
              {docsList}
            </table>

            <!-- Action buttons -->
            <table width="100%" cellpadding="0" cellspacing="0" border="0">
              <tr>
                <td style="padding-right:8px" width="50%">
                  <a href="{validerUrl}" style="display:block;padding:13px;background:#16A34A;color:#ffffff;border-radius:8px;text-decoration:none;font-size:13px;font-weight:700;text-align:center;font-family:Arial,sans-serif">✅ &nbsp;Valider la demande</a>
                </td>
                <td style="padding-left:8px" width="50%">
                  <a href="{rejeterUrl}" style="display:block;padding:13px;background:#F4F4F5;color:#0A0A0A;border-radius:8px;text-decoration:none;font-size:13px;font-weight:700;text-align:center;font-family:Arial,sans-serif;border:1px solid #E4E4E7">Consulter &amp; rejeter</a>
                </td>
              </tr>
            </table>
            """;
        return EnvoyerSmtpAsync(adminEmail, sujet, Layout(body), ct);
    }

    // Confirmation demande reçue (entreprise)
    public Task EnvoyerConfirmationDemandeAsync(string email, string raisonSociale, string reference, string matriculeFiscal, string respPrenom, string respNom, List<string> documentsRecus, CancellationToken ct = default)
    {
        var docsHtml = documentsRecus.Count > 0
            ? string.Join("", documentsRecus.Select(d =>
                $"<tr><td style=\"padding:7px 0;font-size:13px;color:#52525B;border-bottom:1px solid #F4F4F5;font-family:Arial,sans-serif\">✓ &nbsp;{d}</td></tr>"))
            : "<tr><td style=\"padding:7px 0;font-size:13px;color:#A1A1AA;font-family:Arial,sans-serif\">Aucun document soumis</td></tr>";

        var sujet = "Demande reçue — TuniFlow";
        var body = $"""
            {Heading("Demande bien reçue ✓")}
            {Paragraph($"Bonjour <strong>{raisonSociale}</strong>, votre dossier a été soumis et est en cours d'examen par notre équipe.")}

            <!-- Reference block -->
            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FFFBEB;border:1px solid #FFE600;border-radius:10px;margin:0 0 20px">
              <tr>
                <td style="padding:16px 20px">
                  <p style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#A1A1AA;margin:0 0 6px;font-family:Arial,sans-serif">Référence de votre demande</p>
                  <p style="font-size:22px;font-weight:900;color:#0A0A0A;font-family:'Courier New',monospace;letter-spacing:.08em;margin:0">{reference}</p>
                  <p style="font-size:11px;color:#71717A;margin:6px 0 0;font-family:Arial,sans-serif">Conservez cette référence pour tout suivi de dossier.</p>
                </td>
              </tr>
            </table>

            {InfoBox($"""
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {InfoRow("Entreprise", raisonSociale)}
                {InfoRow("Matricule fiscal", $"<span style=\"font-family:'Courier New',monospace\">{matriculeFiscal}</span>")}
                {InfoRow("Responsable", $"{respPrenom} {respNom}")}
                {InfoRow("Délai estimé", "<strong>48h ouvrables</strong>")}
              </table>
            """, "#FAFAFA", "#E4E4E7")}

            <!-- Documents reçus -->
            <p style="font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#A1A1AA;margin:0 0 8px;font-family:Arial,sans-serif">Documents reçus</p>
            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px">
              {docsHtml}
            </table>

            {Divider()}
            {Paragraph("Vous recevrez un e-mail dès que votre dossier aura été traité. Pour toute question, contactez-nous à <a href=\"mailto:support@tuniflow.tn\" style=\"color:#0A0A0A;font-weight:700\">support@tuniflow.tn</a>.")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body, "Votre dossier est en cours d'examen."), ct);
    }

    // Accès validé
    public Task EnvoyerAccesValideAsync(string email, string prenom, string raisonSociale, string loginEmail, string motDePasseTemp, CancellationToken ct = default)
    {
        var loginUrl = $"{FrontendBaseUrl}/login/entreprise";
        var sujet = "Accès validé — TuniFlow";
        var body = $"""
            {AlertBox($"🎉 &nbsp;<strong>Votre accès est confirmé !</strong> Bonjour {prenom}, le compte <strong>{raisonSociale}</strong> est maintenant actif.", "#F0FDF4", "#16A34A", "#14532D")}
            {Heading("Activation de votre compte")}
            {Paragraph("Votre compte TuniFlow est actif. Pour des raisons de securite, aucun mot de passe n'est envoye par e-mail.")}

            <!-- Account block -->
            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#0A0A0A;border-radius:10px;margin:0 0 16px">
              <tr>
                <td style="padding:20px 24px">
                  <p style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.07em;color:#71717A;margin:0 0 4px;font-family:Arial,sans-serif">Identifiant (e-mail)</p>
                  <p style="font-size:15px;color:#E4E4E7;font-family:'Courier New',monospace;font-weight:600;margin:0">{loginEmail}</p>
                </td>
              </tr>
            </table>

            {AlertBox("Pour definir votre mot de passe, cliquez sur <strong>Mot de passe oublie</strong> depuis l'ecran de connexion puis utilisez le code de verification recu par e-mail.", "#FFFBEB", "#FFE600", "#78350F")}

            {PrimaryButton(loginUrl, "Acceder a la plateforme")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body, "Votre accès TuniFlow est activé."), ct);
    }

    // Demande rejetée
    public Task EnvoyerDemandeRejeteeAsync(string email, string prenom, string raisonSociale, string motif, CancellationToken ct = default)
    {
        var sujet = "Demande non acceptée — TuniFlow";
        var body = $"""
            {Heading("Demande non acceptée")}
            {Paragraph($"Bonjour <strong>{prenom}</strong>, après examen de votre dossier, nous ne sommes pas en mesure d'activer le compte de <strong>{raisonSociale}</strong>.")}

            <p style="font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#A1A1AA;margin:0 0 8px;font-family:Arial,sans-serif">Motif du rejet</p>
            {AlertBox(motif, "#FEF2F2", "#EF4444", "#991B1B")}

            {Paragraph("Vous pouvez soumettre une nouvelle demande après avoir corrigé les points mentionnés ci-dessus, ou contacter notre équipe pour plus d'informations.")}
            {Paragraph("<a href=\"mailto:support@tuniflow.tn\" style=\"color:#0A0A0A;font-weight:700\">support@tuniflow.tn</a>")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body), ct);
    }

    // Demande de corrections KYC
    public Task EnvoyerDemandeCorrectionsAsync(
        string email,
        string prenom,
        string raisonSociale,
        IReadOnlyCollection<string> corrections,
        string? messageAdmin,
        CancellationToken ct = default)
    {
        var accessUrl = $"{FrontendBaseUrl}/demande-acces";
        var sujet = "Corrections requises pour votre demande TuniFlow";
        var correctionsHtml = string.Join("", corrections.Select(item =>
            $"""<li style="margin:0 0 10px;color:#52525B;line-height:1.55">{System.Net.WebUtility.HtmlEncode(item)}</li>"""));
        var messageHtml = string.IsNullOrWhiteSpace(messageAdmin)
            ? string.Empty
            : AlertBox(
                System.Net.WebUtility.HtmlEncode(messageAdmin.Trim()),
                "#FFFBEB",
                "#F59E0B",
                "#78350F");

        var body = $"""
            {Heading("Corrections requises")}
            {Paragraph($"Bonjour <strong>{System.Net.WebUtility.HtmlEncode(prenom)}</strong>, votre demande pour <strong>{System.Net.WebUtility.HtmlEncode(raisonSociale)}</strong> est en cours de verification. Nous avons besoin de quelques corrections avant validation.")}

            <p style="font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#A1A1AA;margin:0 0 8px;font-family:Arial,sans-serif">Points a corriger</p>
            {InfoBox($"""
              <ul style="padding-left:20px;margin:0;font-family:Arial,sans-serif">
                {correctionsHtml}
              </ul>
            """, "#FAFAFA", "#F59E0B")}

            {messageHtml}
            {Paragraph("Merci de preparer les pieces corrigees puis de deposer une nouvelle demande avec les informations exactes.")}
            {PrimaryButton(accessUrl, "Corriger ma demande")}
            """;

        return EnvoyerSmtpAsync(email, sujet, Layout(body, "Votre demande TuniFlow necessite des corrections."), ct);
    }

    // Notification facture
    public Task EnvoyerNotifFactureAsync(string email, string nomClient, string sujet, string numeroFacture, string type, decimal montantTtc, CancellationToken ct = default)
    {
        var (couleur, statutLabel, bgLabel) = type switch
        {
            "validee" => ("#16A34A", "Validée", "#F0FDF4"),
            "rejetee" => ("#DC2626", "Rejetée", "#FEF2F2"),
            "payee" => ("#2563EB", "Payée", "#EFF6FF"),
            "rappel" => ("#D97706", "Échéance proche", "#FFFBEB"),
            _ => ("#71717A", type, "#FAFAFA")
        };

        var facturesUrl = $"{FrontendBaseUrl}/factures";
        var body = $"""
            {Heading(sujet)}
            {Paragraph($"Bonjour <strong>{nomClient}</strong>, voici une mise à jour concernant votre facture.")}

            {InfoBox($"""
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                {InfoRow("N° Facture", $"<span style=\"font-family:'Courier New',monospace;font-weight:700\">{numeroFacture}</span>")}
                {InfoRow("Statut", $"<span style=\"background:{bgLabel};color:{couleur};padding:2px 10px;border-radius:99px;font-size:12px;font-weight:700;font-family:Arial,sans-serif\">{statutLabel}</span>")}
                {InfoRow("Montant TTC", $"<span style=\"font-size:16px;font-weight:700;font-family:'Courier New',monospace\">{montantTtc:N3} TND</span>")}
              </table>
            """, "#FAFAFA", "#E4E4E7")}

            {PrimaryButton(facturesUrl, "Voir la facture")}
            """;
        return EnvoyerSmtpAsync(email, sujet, Layout(body), ct);
    }

    // ─── SMTP core ────────────────────────────────────────────────────────────

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
