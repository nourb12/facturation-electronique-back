using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Einvoicing.Infrastructure.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ContextBaseDeDonnees>();
        var hasher = services.GetRequiredService<IPasswordHasher>();
        var jwt = services.GetService<IJwtService>();

        await db.Database.MigrateAsync();

        var hasData = await db.Entreprises.AnyAsync()
            || await db.Utilisateurs.AnyAsync()
            || await db.Factures.AnyAsync();
        if (hasData)
        {
            await TopUpExistingDataAsync(db, hasher, jwt);
            return;
        }

        var now = DateTime.UtcNow;

        var entreprise = Entreprise.Creer(
            "TunisFlow Demo",
            "1234567A/B/M/000",
            "18 Avenue de la Republique",
            "Tunis",
            "1002",
            "contact@tunisflow.tn",
            "TVA-1234567",
            RegimeFiscal.Reel);
        entreprise.MettreAJour(
            entreprise.Nom,
            entreprise.Adresse,
            entreprise.Ville,
            entreprise.CodePostal,
            entreprise.Email,
            "+21671123456",
            "https://tunisflow.tn",
            RegimeFiscal.Reel);

        var motDePasse = "Demo@2026!";

        var superAdmin = Utilisateur.Creer(
            "Amina",
            "Trabelsi",
            "super.admin@tunisflow.tn",
            hasher.Hacher(motDePasse),
            RoleUtilisateur.SuperAdmin);

        var admin = Utilisateur.Creer(
            "Karim",
            "Ayari",
            "admin@tunisflow.tn",
            hasher.Hacher(motDePasse),
            RoleUtilisateur.Admin,
            entreprise.Id);
        admin.MettreAJourProfil("Karim", "Ayari", "+21620990011", "Administrateur", "IT");

        var responsable = Utilisateur.Creer(
            "Leila",
            "Ben Salem",
            "responsable@tunisflow.tn",
            hasher.Hacher(motDePasse),
            RoleUtilisateur.ResponsableEntreprise,
            entreprise.Id);
        responsable.MettreAJourProfil("Leila", "Ben Salem", "+21629888777", "Responsable", "Operations");

        var financier = Utilisateur.Creer(
            "Youssef",
            "Gharbi",
            "finances@tunisflow.tn",
            hasher.Hacher(motDePasse),
            RoleUtilisateur.ResponsableFinancier,
            entreprise.Id);
        financier.MettreAJourProfil("Youssef", "Gharbi", "+21650222333", "DAF", "Finance");

        db.Entreprises.Add(entreprise);
        db.Utilisateurs.AddRange(superAdmin, admin, responsable, financier);

        if (jwt != null)
        {
            var adminRefresh = RefreshToken.Creer(
                admin.Id,
                jwt.GenererRefreshToken(),
                Guid.NewGuid().ToString(),
                "41.226.1.10",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                30);
            var adminSession = SessionActive.Creer(
                admin.Id,
                adminRefresh.Token,
                adminRefresh.UserAgent ?? "Mozilla/5.0",
                adminRefresh.AdresseIp);

            var financierRefresh = RefreshToken.Creer(
                financier.Id,
                jwt.GenererRefreshToken(),
                Guid.NewGuid().ToString(),
                "41.226.1.20",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4)",
                30);
            var financierSession = SessionActive.Creer(
                financier.Id,
                financierRefresh.Token,
                financierRefresh.UserAgent ?? "Mozilla/5.0",
                financierRefresh.AdresseIp);

            db.RefreshTokens.AddRange(adminRefresh, financierRefresh);
            db.Sessions.AddRange(adminSession, financierSession);
        }

        var clientB2B = Client.Creer(
            entreprise.Id,
            "Carthage Telecom",
            "finance@carthage-telecom.tn",
            TypeClient.B2B,
            "6543217B76543",
            "2 Rue de l'Entreprise",
            "Tunis",
            "1073",
            "+21671234567");

        var clientB2C = Client.Creer(
            entreprise.Id,
            "Sana Ben Romdhane",
            "sana.benromdhane@gmail.com",
            TypeClient.B2C,
            null,
            "Residence Les Jardins",
            "Ariana",
            "2083",
            "+21698111222");

        var clientB2G = Client.Creer(
            entreprise.Id,
            "Ministere du Tourisme",
            "compta@tourisme.gov.tn",
            TypeClient.B2G,
            "9988776C11223",
            "1 Avenue Mohamed V",
            "Tunis",
            "1000",
            "+21671765432");

        var catServices = CategorieProduit.Creer(entreprise.Id, "Services cloud", "Abonnements SaaS et support");
        var catMateriel = CategorieProduit.Creer(entreprise.Id, "Materiel", "Equipements informatiques");

        var produitSaas = Produit.Creer(
            entreprise.Id,
            "SAAS-ERP",
            "Abonnement ERP mensuel",
            850m,
            19m,
            TypeProduit.Service,
            catServices.Id,
            "Licence multi-tenant",
            "MOIS");

        var produitSupport = Produit.Creer(
            entreprise.Id,
            "SUPPORT-PLAT",
            "Support premium 24/7",
            400m,
            19m,
            TypeProduit.Service,
            catServices.Id,
            "SLA critique",
            "AN");

        var produitLicOffice = Produit.Creer(
            entreprise.Id,
            "LIC-OFFICE",
            "Pack bureautique annuel",
            250m,
            7m,
            TypeProduit.Service,
            catServices.Id,
            "Licence par poste",
            "AN");

        var produitAudit = Produit.Creer(
            entreprise.Id,
            "AUDIT-SEC",
            "Audit de securite IT",
            1200m,
            19m,
            TypeProduit.Service,
            catServices.Id,
            "Rapport complet avec recommandations",
            "JOUR");

        var produitLaptop = Produit.Creer(
            entreprise.Id,
            "HW-LAPTOP",
            "Laptop Pro 14 i7",
            3500m,
            19m,
            TypeProduit.Produit,
            catMateriel.Id,
            "14 pouces, 16 Go RAM, 512 Go SSD",
            "PIECE");

        db.Clients.AddRange(clientB2B, clientB2C, clientB2G);
        db.Categories.AddRange(catServices, catMateriel);
        db.Produits.AddRange(produitSaas, produitSupport, produitLicOffice, produitAudit, produitLaptop);

        await db.SaveChangesAsync();

        var compteur = await GetOrCreateCompteurFactureAsync(db, entreprise.Id, now.Year, now.Month);
        await db.SaveChangesAsync();

        string NextNumero()
        {
            var numero = compteur.Incrementer();
            db.CompteurFactures.Update(compteur);
            return numero;
        }

        Facture BuildFacture(
            Client client,
            Utilisateur auteur,
            ModePaiement mode,
            TypeFacture type,
            DateTime echeance,
            string? reference,
            string? notes,
            params (Produit produit, decimal quantite, decimal remise)[] lignes)
        {
            var facture = Facture.Creer(
                entreprise.Id,
                client.Id,
                auteur.Id,
                NextNumero(),
                type,
                mode,
                echeance,
                reference,
                notes,
                "Paiement a 30 jours",
                "TND");

            var ordre = 1;
            foreach (var ligne in lignes)
            {
                var lf = LigneFacture.Creer(
                    facture.Id,
                    ordre++,
                    ligne.produit.Libelle,
                    ligne.quantite,
                    ligne.produit.PrixUnitaire,
                    ligne.produit.TauxTva,
                    ligne.produit.Id,
                    ligne.produit.Description,
                    ligne.produit.Unite,
                    ligne.remise);
                facture.AjouterLigne(lf);
            }

            return facture;
        }

        var facturePayee = BuildFacture(
            clientB2B,
            admin,
            ModePaiement.Virement,
            TypeFacture.Facture,
            now.AddDays(30),
            "BC-2026-001",
            "Licence ERP + support",
            (produitSaas, 1m, 0),
            (produitSupport, 1m, 0),
            (produitLicOffice, 10m, 5));
        facturePayee.ValiderMetier(admin.Id);
        facturePayee.MarquerConforme(admin.Id, "2024");
        facturePayee.MarquerTransmise(admin.Id);
        facturePayee.MarquerAcceptee(superAdmin.Id);
        facturePayee.EnregistrerPaiement(facturePayee.TotalTtc, financier.Id);

        var paiementPayee = Paiement.Creer(
            facturePayee.Id,
            entreprise.Id,
            financier.Id,
            facturePayee.TotalTtc,
            ModePaiement.Virement,
            DateTime.UtcNow,
            "VIR-202603-001",
            "Amen Bank",
            "Reglement total facture acceptee");

        var signaturePayee = SignatureRequest.Creer(facturePayee.Id, entreprise.Id, admin.Id);
        signaturePayee.Signer("SIG-DEMO-001", "CERT-ERP-001");

        var echangePayee = ExternalExchange.Creer(facturePayee.Id, entreprise.Id, admin.Id);
        echangePayee.MarquerEnvoye();
        echangePayee.MarquerAcknowledge();
        echangePayee.MarquerAccepte();

        var facturePartielle = BuildFacture(
            clientB2G,
            responsable,
            ModePaiement.Cheque,
            TypeFacture.Facture,
            now.AddDays(25),
            "BC-2026-002",
            "Provision parc informatique",
            (produitLaptop, 1m, 0),
            (produitSaas, 1m, 0));
        facturePartielle.ValiderMetier(responsable.Id);
        facturePartielle.MarquerConforme(responsable.Id, "2024");
        facturePartielle.MarquerTransmise(responsable.Id);
        facturePartielle.MarquerAcceptee(admin.Id);
        var acompte = Math.Round(facturePartielle.TotalTtc * 0.4m, 3);
        facturePartielle.EnregistrerPaiement(acompte, financier.Id);

        var paiementPartiel = Paiement.Creer(
            facturePartielle.Id,
            entreprise.Id,
            financier.Id,
            acompte,
            ModePaiement.Cheque,
            DateTime.UtcNow.AddDays(-1),
            "CHQ-2026-045",
            "BIAT",
            "Acompte de demarrage");

        var echangePartiel = ExternalExchange.Creer(facturePartielle.Id, entreprise.Id, responsable.Id);
        echangePartiel.MarquerEnvoye();
        echangePartiel.MarquerAcknowledge();

        var factureRejetee = BuildFacture(
            clientB2B,
            admin,
            ModePaiement.CarteBancaire,
            TypeFacture.Facture,
            now.AddDays(20),
            "BC-2026-003",
            "Audit et remediation",
            (produitAudit, 1m, 0));
        factureRejetee.ValiderMetier(admin.Id);
        factureRejetee.MarquerConforme(admin.Id, "2024");
        factureRejetee.MarquerTransmise(admin.Id);
        factureRejetee.MarquerRejetee(superAdmin.Id, "Incoherence sur le taux de TVA declare");

        var echangeRejete = ExternalExchange.Creer(factureRejetee.Id, entreprise.Id, admin.Id);
        echangeRejete.MarquerEnvoye();
        echangeRejete.MarquerRejete("422", "Validation TEIF refusee", "TVA non alignee");

        var factureAnnulee = BuildFacture(
            clientB2C,
            responsable,
            ModePaiement.Especes,
            TypeFacture.Facture,
            now.AddDays(10),
            "BC-2026-004",
            "Licence personnelle",
            (produitLicOffice, 1m, 0));
        factureAnnulee.ValiderMetier(responsable.Id);
        factureAnnulee.Annuler(responsable.Id, "Commande annulee par le client avant livraison");

        var factureBrouillon = BuildFacture(
            clientB2B,
            admin,
            ModePaiement.Virement,
            TypeFacture.Proforma,
            now.AddDays(45),
            "PRO-2026-005",
            "Proposition renouvellement licences",
            (produitSaas, 1m, 0),
            (produitSupport, 1m, 0));

        db.Factures.AddRange(facturePayee, facturePartielle, factureRejetee, factureAnnulee, factureBrouillon);
        db.Paiements.AddRange(paiementPayee, paiementPartiel);
        db.Signatures.Add(signaturePayee);
        db.Echanges.AddRange(echangePayee, echangePartiel, echangeRejete);

        await EnsureAvoirsSeedAsync(db, entreprise, admin, now);

        await db.SaveChangesAsync();
    }

    private static async Task TopUpExistingDataAsync(
        ContextBaseDeDonnees db,
        IPasswordHasher hasher,
        IJwtService? jwt)
    {
        var entreprise = await db.Entreprises
            .OrderByDescending(e => e.ModifieLe)
            .ThenByDescending(e => e.CreeLe)
            .FirstOrDefaultAsync();

        if (entreprise is null)
            return;

        var now = DateTime.UtcNow;
        var utilisateurs = await db.Utilisateurs
            .Where(u => u.EntrepriseId == entreprise.Id)
            .OrderBy(u => u.CreeLe)
            .ToListAsync();

        if (utilisateurs.Count == 0)
        {
            var rattache = await db.Utilisateurs
                .OrderBy(u => u.CreeLe)
                .FirstOrDefaultAsync();

            if (rattache is not null)
            {
                rattache.RattacherEntreprise(entreprise.Id);
                utilisateurs.Add(rattache);
            }
            else
            {
                var admin = Utilisateur.Creer(
                    "Equipe",
                    "Facturation",
                    $"admin.{entreprise.Id:N}@local.eyinvoice",
                    hasher.Hacher("Demo@2026!"),
                    RoleUtilisateur.Admin,
                    entreprise.Id);
                admin.MettreAJourProfil("Equipe", "Facturation", entreprise.Telephone, "Administrateur", "Finance");
                db.Utilisateurs.Add(admin);
                utilisateurs.Add(admin);
            }
        }

        foreach (var utilisateur in utilisateurs)
        {
            var prenom = string.IsNullOrWhiteSpace(utilisateur.Prenom) ? "Equipe" : utilisateur.Prenom;
            var nom = string.IsNullOrWhiteSpace(utilisateur.Nom) ? entreprise.Nom : utilisateur.Nom;
            var telephone = string.IsNullOrWhiteSpace(utilisateur.Telephone) ? entreprise.Telephone : utilisateur.Telephone;
            var poste = string.IsNullOrWhiteSpace(utilisateur.Poste) ? PosteParRole(utilisateur.Role) : utilisateur.Poste;
            var departement = string.IsNullOrWhiteSpace(utilisateur.Departement) ? DepartementParRole(utilisateur.Role) : utilisateur.Departement;

            utilisateur.MettreAJourProfil(prenom, nom, telephone, poste, departement);
            utilisateur.ConfigurerSecurite(true);
        }

        var auteur = utilisateurs
            .OrderBy(u => u.Role == RoleUtilisateur.Admin ? 0 : 1)
            .ThenBy(u => u.Role == RoleUtilisateur.ResponsableEntreprise ? 0 : 1)
            .ThenBy(u => u.CreeLe)
            .First();
        var financier = utilisateurs
            .OrderBy(u => u.Role == RoleUtilisateur.ResponsableFinancier ? 0 : 1)
            .ThenBy(u => u.CreeLe)
            .First();

        if (jwt is not null)
        {
            foreach (var utilisateur in utilisateurs.Where(u => !db.RefreshTokens.Any(r => r.UtilisateurId == u.Id)))
            {
                var refresh = RefreshToken.Creer(
                    utilisateur.Id,
                    jwt.GenererRefreshToken(),
                    Guid.NewGuid().ToString(),
                    "102.168.12.10",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    30);
                var session = SessionActive.Creer(
                    utilisateur.Id,
                    refresh.Token,
                    refresh.UserAgent ?? "Mozilla/5.0",
                    refresh.AdresseIp);

                db.RefreshTokens.Add(refresh);
                db.Sessions.Add(session);
            }
        }

        var profil = new
        {
            raisonSociale = string.IsNullOrWhiteSpace(entreprise.Nom) ? "Entreprise" : entreprise.Nom,
            nomCommercial = entreprise.Nom,
            forme = DeduireForme(entreprise.Nom),
            capital = "10000 TND",
            dateCreation = entreprise.CreeLe.ToString("yyyy-MM-dd"),
            activiteCode = "6201Z",
            activiteLibelle = "Edition de logiciels et services numeriques",
            gouvernorat = entreprise.Ville,
            pays = "Tunisie",
            fax = entreprise.Telephone,
            numRne = $"RNE-{entreprise.MatriculeFiscal}",
            regimeTva = ConvertirRegimeFiscal(entreprise.RegimeFiscal),
            tauxTvaPrincipal = 19m
        };
        entreprise.DefinirDonneesInscription(JsonSerializer.Serialize(profil));
        entreprise.ConfigurerTeif(
            JsonSerializer.Serialize(new
            {
                signature = true,
                archivage = true,
                horodatage = false,
                sandbox = true,
                surveille = true
            }),
            string.IsNullOrWhiteSpace(entreprise.VersionTeif) ? "2024" : entreprise.VersionTeif);

        await EnsureClientsAsync(db, entreprise.Id);
        await EnsureCatalogueAsync(db, entreprise.Id);
        await EnsureFiscalSettingsAsync(db, entreprise.Id);
        await EnsurePersonnalisationAsync(db, entreprise);
        await db.SaveChangesAsync();

        await EnsureInvoicesAsync(db, entreprise, auteur, financier, now);
        await EnsureAvoirsSeedAsync(db, entreprise, auteur, now);
        await db.SaveChangesAsync();

        var score = CalculerScore(db, entreprise.Id);
        entreprise.DefinirScoring(score.score, JsonSerializer.Serialize(score.details));
        await db.SaveChangesAsync();
    }

    private static async Task EnsureClientsAsync(ContextBaseDeDonnees db, Guid entrepriseId)
    {
        if (await db.Clients.AnyAsync(c => c.EntrepriseId == entrepriseId))
            return;

        db.Clients.AddRange(
            Client.Creer(entrepriseId, "Societe Atlas Services", "comptabilite@atlas-services.tn", TypeClient.B2B, "8172634A", "14 Rue du Lac", "Tunis", "1053", "+21670011022"),
            Client.Creer(entrepriseId, "Direction regionale Equipement", "marches@equipement.gov.tn", TypeClient.B2G, "1432007B", "8 Avenue Hedi Chaker", "Sfax", "3000", "+21674455667"),
            Client.Creer(entrepriseId, "Ines Mansouri", "ines.mansouri@gmail.com", TypeClient.B2C, null, "Residence Ennasr", "Ariana", "2037", "+21655066778"));
    }

    private static async Task EnsureCatalogueAsync(ContextBaseDeDonnees db, Guid entrepriseId)
    {
        var categories = await db.Categories.Where(c => c.EntrepriseId == entrepriseId).ToListAsync();
        var produits = await db.Produits.Where(p => p.EntrepriseId == entrepriseId).ToListAsync();

        var services = categories.FirstOrDefault(c => c.Nom == "Services")
            ?? CategorieProduit.Creer(entrepriseId, "Services", "Prestations facturees recurrentes");
        var equipements = categories.FirstOrDefault(c => c.Nom == "Equipements")
            ?? CategorieProduit.Creer(entrepriseId, "Equipements", "Materiels et accessoires");

        if (!categories.Any(c => c.Id == services.Id)) db.Categories.Add(services);
        if (!categories.Any(c => c.Id == equipements.Id)) db.Categories.Add(equipements);

        if (!produits.Any(p => p.Code == "ABO-MENSUEL"))
            db.Produits.Add(Produit.Creer(entrepriseId, "ABO-MENSUEL", "Abonnement mensuel", 890m, 19m, TypeProduit.Service, services.Id, "Abonnement principal facture mensuellement", "MOIS"));
        if (!produits.Any(p => p.Code == "SUPPORT-PRO"))
            db.Produits.Add(Produit.Creer(entrepriseId, "SUPPORT-PRO", "Support prioritaire", 240m, 19m, TypeProduit.Service, services.Id, "Assistance et support utilisateurs", "MOIS"));
        if (!produits.Any(p => p.Code == "PARAM-INIT"))
            db.Produits.Add(Produit.Creer(entrepriseId, "PARAM-INIT", "Parametrage initial", 1250m, 19m, TypeProduit.Service, services.Id, "Activation et accompagnement de demarrage", "FORFAIT"));
        if (!produits.Any(p => p.Code == "SCAN-A4"))
            db.Produits.Add(Produit.Creer(entrepriseId, "SCAN-A4", "Scanner documentaire A4", 640m, 19m, TypeProduit.Produit, equipements.Id, "Materiel de numerisation pour justificatifs", "PIECE"));
    }

    private static async Task EnsureFiscalSettingsAsync(ContextBaseDeDonnees db, Guid entrepriseId)
    {
        if (!await db.Taxes.AnyAsync(t => t.EntrepriseId == entrepriseId))
        {
            db.Taxes.AddRange(
                Taxe.Creer(entrepriseId, "TVA 19%", 19m, TypeTaxe.Tva, "Taux standard"),
                Taxe.Creer(entrepriseId, "TVA 7%", 7m, TypeTaxe.Tva, "Taux reduit"),
                Taxe.Creer(entrepriseId, "FODEC", 1m, TypeTaxe.Fodec, "Contribution parafiscale"));
        }

        if (!await db.ParametresFiscaux.AnyAsync(p => p.EntrepriseId == entrepriseId))
        {
            db.ParametresFiscaux.AddRange(
                ParametreFiscal.Creer(entrepriseId, "Retenue a la source", 1.5m, TypeParametreFiscal.Pourcentage, SigneParametreFiscal.Negatif, OrdreCalcul.ApresTva, UtilisationParametreFiscal.Auto, true, new List<string> { "Facture", "Avoir" }),
                ParametreFiscal.Creer(entrepriseId, "Timbre fiscal", 1m, TypeParametreFiscal.Fixe, SigneParametreFiscal.Positif, OrdreCalcul.ApresTva, UtilisationParametreFiscal.Auto, false, new List<string> { "Facture" }));
        }
    }

    private static async Task EnsurePersonnalisationAsync(ContextBaseDeDonnees db, Entreprise entreprise)
    {
        var personnalisation = await db.Personnalisations.FirstOrDefaultAsync(p => p.EntrepriseId == entreprise.Id);
        var json = JsonSerializer.Serialize(new
        {
            branding = new
            {
                companyName = entreprise.Nom,
                accent = "#111827",
                secondary = "#C8A45D",
                density = "comfortable"
            },
            facture = new
            {
                footer = "Merci pour votre confiance.",
                showReference = true,
                showConditions = true
            }
        });

        if (personnalisation is null)
            db.Personnalisations.Add(Personnalisation.Creer(entreprise.Id, json));
        else
            personnalisation.MettreAJour(json);
    }

    private static async Task EnsureInvoicesAsync(
        ContextBaseDeDonnees db,
        Entreprise entreprise,
        Utilisateur auteur,
        Utilisateur financier,
        DateTime now)
    {
        if (await db.Factures.AnyAsync(f => f.EntrepriseId == entreprise.Id))
            return;

        var clients = await db.Clients.Where(c => c.EntrepriseId == entreprise.Id).OrderBy(c => c.CreeLe).ToListAsync();
        var produits = await db.Produits.Where(p => p.EntrepriseId == entreprise.Id).OrderBy(p => p.CreeLe).ToListAsync();
        if (clients.Count < 3 || produits.Count < 3)
            return;

        var compteur = await GetOrCreateCompteurFactureAsync(db, entreprise.Id, now.Year, now.Month);

        string NextNumero() => compteur.Incrementer();

        Facture BuildFacture(Client client, DateTime echeance, ModePaiement mode, TypeFacture type, params (Produit produit, decimal quantite, decimal remise)[] lignes)
        {
            var facture = Facture.Creer(entreprise.Id, client.Id, auteur.Id, NextNumero(), type, mode, echeance, null, null, "Paiement a 30 jours", entreprise.DevisePrincipale);
            var ordre = 1;
            foreach (var ligne in lignes)
            {
                facture.AjouterLigne(LigneFacture.Creer(facture.Id, ordre++, ligne.produit.Libelle, ligne.quantite, ligne.produit.PrixUnitaire, ligne.produit.TauxTva, ligne.produit.Id, ligne.produit.Description, ligne.produit.Unite, ligne.remise));
            }
            return facture;
        }

        var facturePayee = BuildFacture(clients[0], now.AddDays(15), ModePaiement.Virement, TypeFacture.Facture, (produits[0], 1m, 0), (produits[1], 1m, 0));
        facturePayee.ValiderMetier(auteur.Id);
        facturePayee.MarquerConforme(auteur.Id, entreprise.VersionTeif);
        facturePayee.MarquerTransmise(auteur.Id);
        facturePayee.MarquerAcceptee(auteur.Id);
        facturePayee.EnregistrerPaiement(facturePayee.TotalTtc, financier.Id);

        var paiement = Paiement.Creer(facturePayee.Id, entreprise.Id, financier.Id, facturePayee.TotalTtc, ModePaiement.Virement, now.AddDays(-2), "VIR-SEED-001", "BIAT", "Reglement complet");
        var signature = SignatureRequest.Creer(facturePayee.Id, entreprise.Id, auteur.Id);
        signature.Signer("SIG-REAL-USAGE-001", "CERT-001");
        var echange = ExternalExchange.Creer(facturePayee.Id, entreprise.Id, auteur.Id);
        echange.MarquerEnvoye();
        echange.MarquerAcknowledge();
        echange.MarquerAccepte();

        var facturePartielle = BuildFacture(clients[1], now.AddDays(20), ModePaiement.Cheque, TypeFacture.Facture, (produits[2], 1m, 0), (produits[3], 2m, 0));
        facturePartielle.ValiderMetier(auteur.Id);
        facturePartielle.MarquerConforme(auteur.Id, entreprise.VersionTeif);
        facturePartielle.MarquerTransmise(auteur.Id);
        facturePartielle.MarquerAcceptee(auteur.Id);
        facturePartielle.EnregistrerPaiement(Math.Round(facturePartielle.TotalTtc * 0.35m, 3), financier.Id);

        var factureRejetee = BuildFacture(clients[2], now.AddDays(10), ModePaiement.CarteBancaire, TypeFacture.Facture, (produits[0], 1m, 0));
        factureRejetee.ValiderMetier(auteur.Id);
        factureRejetee.MarquerConforme(auteur.Id, entreprise.VersionTeif);
        factureRejetee.MarquerTransmise(auteur.Id);
        factureRejetee.MarquerRejetee(auteur.Id, "Controle TVA a reprendre");

        var factureBrouillon = BuildFacture(clients[0], now.AddDays(30), ModePaiement.Virement, TypeFacture.Proforma, (produits[1], 1m, 0));

        db.Factures.AddRange(facturePayee, facturePartielle, factureRejetee, factureBrouillon);
        db.Paiements.Add(paiement);
        db.Signatures.Add(signature);
        db.Echanges.Add(echange);
    }

    private static async Task EnsureAvoirsSeedAsync(
        ContextBaseDeDonnees db,
        Entreprise entreprise,
        Utilisateur auteur,
        DateTime now)
    {
        const int cible = 7;
        var avoirCount = await db.Factures.CountAsync(f => f.EntrepriseId == entreprise.Id && f.TypeFacture == TypeFacture.Avoir);
        if (avoirCount >= cible)
            return;

        var clients = await db.Clients.Where(c => c.EntrepriseId == entreprise.Id).OrderBy(c => c.CreeLe).ToListAsync();
        var produits = await db.Produits.Where(p => p.EntrepriseId == entreprise.Id).OrderBy(p => p.CreeLe).ToListAsync();
        if (clients.Count == 0 || produits.Count == 0)
            return;

        var compteur = await GetOrCreateCompteurFactureAsync(db, entreprise.Id, now.Year, now.Month);

        string NextNumero() => compteur.Incrementer();

        var modes = new[]
        {
            ModePaiement.Virement,
            ModePaiement.Cheque,
            ModePaiement.Especes,
            ModePaiement.CarteBancaire,
            ModePaiement.Traite
        };

        var aAjouter = cible - avoirCount;
        var liste = new List<Facture>(aAjouter);
        for (var i = 0; i < aAjouter; i++)
        {
            var client = clients[i % clients.Count];
            var produit = produits[i % produits.Count];
            var echeance = now.AddDays(30 + (i * 3));
            var avoir = Facture.Creer(
                entreprise.Id,
                client.Id,
                auteur.Id,
                NextNumero(),
                TypeFacture.Avoir,
                modes[i % modes.Length],
                echeance,
                $"AVOIR-SEED-{avoirCount + i + 1:D3}",
                $"Avoir de demonstration (seed) n {avoirCount + i + 1}",
                "Paiement a 30 jours",
                entreprise.DevisePrincipale);

            var qte = 1m + (i * 0.1m);
            avoir.AjouterLigne(LigneFacture.Creer(
                avoir.Id,
                1,
                produit.Libelle,
                qte,
                produit.PrixUnitaire,
                produit.TauxTva,
                produit.Id,
                produit.Description,
                produit.Unite,
                0));

            if (i % 3 == 1)
                avoir.ValiderMetier(auteur.Id);

            liste.Add(avoir);
        }

        db.Factures.AddRange(liste);
    }

    private static (int score, object details) CalculerScore(ContextBaseDeDonnees db, Guid entrepriseId)
    {
        var clients = db.Clients.Count(c => c.EntrepriseId == entrepriseId);
        var produits = db.Produits.Count(p => p.EntrepriseId == entrepriseId);
        var factures = db.Factures.Count(f => f.EntrepriseId == entrepriseId);
        var paiements = db.Paiements.Count(p => p.EntrepriseId == entrepriseId);
        var parametrage = db.Taxes.Count(t => t.EntrepriseId == entrepriseId) + db.ParametresFiscaux.Count(p => p.EntrepriseId == entrepriseId);
        var score = Math.Min(100, 35 + (clients * 8) + (produits * 6) + (factures * 7) + (paiements * 5) + (parametrage * 3));
        return (score, new
        {
            clients,
            produits,
            factures,
            paiements,
            parametrage
        });
    }

    private static async Task<CompteurFacture> GetOrCreateCompteurFactureAsync(
        ContextBaseDeDonnees db,
        Guid entrepriseId,
        int annee,
        int mois)
    {
        var tracked = db.CompteurFactures.Local
            .FirstOrDefault(c => c.EntrepriseId == entrepriseId && c.Annee == annee && c.Mois == mois);
        if (tracked is not null)
            return tracked;

        var persisted = await db.CompteurFactures
            .FirstOrDefaultAsync(c => c.EntrepriseId == entrepriseId && c.Annee == annee && c.Mois == mois);
        if (persisted is not null)
            return persisted;

        var compteur = CompteurFacture.Creer(entrepriseId, annee, mois);
        db.CompteurFactures.Add(compteur);
        return compteur;
    }

    private static string PosteParRole(RoleUtilisateur role)
        => role switch
        {
            RoleUtilisateur.SuperAdmin => "Super administrateur",
            RoleUtilisateur.ResponsableEntreprise => "Responsable entreprise",
            RoleUtilisateur.ResponsableFinancier => "Responsable financier",
            _ => "Administrateur"
        };

    private static string DepartementParRole(RoleUtilisateur role)
        => role switch
        {
            RoleUtilisateur.ResponsableFinancier => "Finance",
            RoleUtilisateur.ResponsableEntreprise => "Operations",
            _ => "Administration"
        };

    private static string DeduireForme(string nom)
    {
        var upper = (nom ?? string.Empty).ToUpperInvariant();
        if (upper.Contains("SUARL")) return "SUARL";
        if (upper.Contains("SAS")) return "SAS";
        if (upper.Contains("SA")) return "SA";
        return "SARL";
    }

    private static string ConvertirRegimeFiscal(RegimeFiscal regimeFiscal)
        => regimeFiscal switch
        {
            RegimeFiscal.Forfait => "Regime forfaitaire",
            RegimeFiscal.NonAssujetti => "Exonere",
            _ => "Regime reel"
        };
}
