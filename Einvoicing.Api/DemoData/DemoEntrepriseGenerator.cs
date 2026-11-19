using System.Globalization;
using System.Text.Json;
using Einvoicing.Api.Controllers;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.DemoData;

/// <summary>
/// Construit six mois d'activité réaliste pour l'entreprise du compte responsable@tuniflow.tn.
/// Chaque document passe par les services métier réels (validation, XML TEIF, signature,
/// échange TTN, paiements, OCR) puis ses horodatages sont replacés à la date de l'opération.
/// Lancement : dotnet run --project Einvoicing.Api -- --generer-demo
/// </summary>
public sealed class DemoEntrepriseGenerator
{
    public const string CleMarqueur = "demo.historique.";
    private const string EmailResponsable = "responsable@tuniflow.tn";

    private readonly IServiceProvider _sp;
    private readonly ContextBaseDeDonnees _db;
    private readonly ILogger _log;
    private readonly string _assets;
    private readonly string _uploadsRoot;
    private readonly Random _rng = new(2026);
    private readonly DateTime _runStart = DateTime.UtcNow.AddSeconds(-2);
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly DateTime _debut;
    private readonly Dictionary<(int, int), CompteurFacture> _compteurs = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private Entreprise _entreprise = null!;
    private Guid _eid;
    private Utilisateur _responsable = null!, _admin = null!, _financier = null!, _ventes = null!, _commercial2 = null!;
    private readonly List<ClientDemo> _clients = new();
    private readonly List<Produit> _produits = new();
    private readonly List<Fournisseur> _fournisseurs = new();
    private readonly List<Facture> _factures = new();
    private readonly List<(DateTime Quand, Utilisateur Qui, string Methode, string Ressource, string Chemin)> _audit = new();

    private sealed record ClientDemo(Client Client, int RetardMin, int RetardMax, int Delai, bool Rs, string[] Familles, int Poids);

    private DemoEntrepriseGenerator(IServiceProvider sp, IWebHostEnvironment env, ILogger log)
    {
        _sp = sp;
        _db = sp.GetRequiredService<ContextBaseDeDonnees>();
        _log = log;
        _assets = Path.Combine(env.ContentRootPath, "DemoData", "Assets");
        _uploadsRoot = Path.Combine(env.ContentRootPath, "uploads");
        _debut = new DateTime(_now.Year, _now.Month, _now.Day, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6).AddDays(1);
    }

    public static async Task ExecuterAsync(IServiceProvider sp, IWebHostEnvironment env, ILogger log)
    {
        var g = new DemoEntrepriseGenerator(sp, env, log);
        await g.RunAsync();
    }

    private async Task RunAsync()
    {
        _log.LogInformation("Génération de l'historique de démonstration ({Debut:d} → {Fin:d})", _debut, _now);
        await ChargerComptesAsync();
        await PurgerAsync();
        await IdentiteAsync();
        await CatalogueAsync();
        await TiersAsync();
        await VentesAsync();
        await DocumentsCommerciauxAsync();
        await AchatsAsync();
        await TresorerieAsync();
        await ScansAsync();
        await PaieEtComptabiliteAsync();
        await CalendrierAsync();
        await AuditEtComptesAsync();
        _log.LogInformation("Historique généré : {Factures} factures, {Clients} clients, {Fournisseurs} fournisseurs.",
            _factures.Count, _clients.Count, _fournisseurs.Count);
    }

    // ------------------------------------------------------------------ outils

    private DateTime J(DateTime d, int heure, int minute = 0)
        => new(d.Year, d.Month, d.Day, heure, minute, 0, DateTimeKind.Utc);

    private DateTime Ouvre(DateTime d)
    {
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d;
    }

    private void Set(object entite, string propriete, object? valeur)
    {
        var entry = _db.Entry(entite);
        if (entry.Metadata.FindProperty(propriete) is not null)
            entry.Property(propriete).CurrentValue = valeur;
    }

    /// <summary>Replace tous les horodatages "maintenant" produits par la dernière opération.</summary>
    private async Task StampAsync(DateTime cible)
    {
        cible = DateTime.SpecifyKind(cible, DateTimeKind.Utc);
        var limite = DateTime.UtcNow.AddSeconds(5);
        var k = 0;
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            foreach (var p in entry.Properties)
            {
                if (p.CurrentValue is DateTime dt && dt >= _runStart && dt <= limite)
                    p.CurrentValue = cible.AddMilliseconds(k++);
            }
        }
        await _db.SaveChangesAsync();
    }

    private string Numero(DateTime emission)
    {
        var cle = (emission.Year, emission.Month);
        if (!_compteurs.TryGetValue(cle, out var c))
        {
            c = CompteurFacture.Creer(_eid, emission.Year, emission.Month);
            _db.CompteurFactures.Add(c);
            _compteurs[cle] = c;
        }
        return c.Incrementer();
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private void Audit(DateTime quand, Utilisateur qui, string methode, string ressource, string chemin)
        => _audit.Add((quand, qui, methode, ressource, chemin));

    // ------------------------------------------------------------------ comptes

    private async Task ChargerComptesAsync()
    {
        _responsable = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == EmailResponsable)
            ?? throw new InvalidOperationException("Le compte responsable@tuniflow.tn est introuvable. Démarrez l'API une fois pour créer les comptes de démo.");
        _eid = _responsable.EntrepriseId ?? throw new InvalidOperationException("Le responsable n'est rattaché à aucune entreprise.");
        _entreprise = await _db.Entreprises.FirstAsync(e => e.Id == _eid);
        _admin = await _db.Utilisateurs.FirstAsync(u => u.Email == "admin@tuniflow.tn");
        _financier = await _db.Utilisateurs.FirstAsync(u => u.Email == "finances@tuniflow.tn");
        _ventes = await _db.Utilisateurs.FirstAsync(u => u.Email == "ventes@tuniflow.tn");
        foreach (var u in new[] { _admin, _financier, _ventes })
            if (u.EntrepriseId != _eid) u.RattacherEntreprise(_eid);
    }

    private async Task PurgerAsync()
    {
        var e = _eid;
        var factureIds = _db.Factures.Where(f => f.EntrepriseId == e).Select(f => f.Id);
        await _db.Echanges.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Signatures.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Paiements.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.ScannedDocuments.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.PaiementsFournisseur.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.FacturesFournisseur.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.DocumentsCommerciaux.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Transactions.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.HistoriqueFactures.Where(x => factureIds.Contains(x.FactureId)).ExecuteDeleteAsync();
        await _db.LignesFacture.Where(x => factureIds.Contains(x.FactureId)).ExecuteDeleteAsync();
        await _db.Factures.Where(x => x.EntrepriseId == e && x.FactureOrigineId != null).ExecuteDeleteAsync();
        await _db.Factures.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.CompteurFactures.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Produits.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Categories.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Clients.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.Fournisseurs.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.CalendrierEvents.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.CalendrierTasks.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.BulletinsPaie.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.EmployesPaie.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.EcrituresComptables.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.ActifsComptables.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.ExercicesComptables.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        await _db.EvenementsAudit.Where(x => x.EntrepriseId == e).ExecuteDeleteAsync();
        _log.LogInformation("Anciennes données de l'entreprise supprimées.");
    }

    // ------------------------------------------------------------------ identité

    private string DataUri(string fichier)
    {
        var bytes = File.ReadAllBytes(Path.Combine(_assets, fichier));
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    private async Task IdentiteAsync()
    {
        _entreprise.MettreAJour("Néoflux Solutions SARL",
            "Immeuble Le Prestige, Rue du Lac Biwa, Les Berges du Lac", "Tunis", "1053",
            "contact@neoflux.tn", "+216 71 963 480", "https://www.neoflux.tn", RegimeFiscal.Reel, "TND");
        Set(_entreprise, nameof(Entreprise.MatriculeFiscal), "1587342TAM000");
        Set(_entreprise, "CodeTva", "1587342/T/A/M/000");
        Set(_entreprise, nameof(Entreprise.CreeLe), new DateTime(2025, 3, 12, 9, 0, 0, DateTimeKind.Utc));

        Directory.CreateDirectory(Path.Combine(_uploadsRoot, "logos"));
        File.Copy(Path.Combine(_assets, "logo.png"), Path.Combine(_uploadsRoot, "logos", "neoflux-logo.png"), true);
        _entreprise.DefinirLogo("uploads/logos/neoflux-logo.png");

        _entreprise.DefinirDonneesInscription(JsonSerializer.Serialize(new
        {
            raisonSociale = "Néoflux Solutions SARL",
            nomCommercial = "Néoflux Solutions",
            forme = "SARL",
            capital = "150000 TND",
            dateCreation = "2019-04-15",
            activiteCode = "6202A",
            activiteLibelle = "Intégration ERP, développement logiciel et services informatiques",
            gouvernorat = "Tunis",
            pays = "Tunisie",
            fax = "+216 71 963 481",
            numRne = "B0158734212019",
            regimeTva = "Réel",
            tauxTvaPrincipal = 19m,
            effectif = 12,
            representantLegal = "Leila Ben Salem",
            banque = "BIAT - Agence Les Berges du Lac",
            rib = "08 006 0000123456789 47"
        }));
        _entreprise.ConfigurerTeif(JsonSerializer.Serialize(new { signature = true, archivage = true, horodatage = true, sandbox = true, surveille = true }), "2024");

        var logo = DataUri("logo.png");
        var personnalisation = new
        {
            branding = new { companyName = "Néoflux Solutions SARL", accent = "#162B54", secondary = "#0096A0", density = "comfortable" },
            facture = new { footer = "Merci pour votre confiance.", showReference = true, showConditions = true },
            pdf = new
            {
                logoUrl = logo,
                primaryColor = "#162B54",
                font = "Helvetica",
                paperSize = "A4",
                matriculeFiscal = "1587342/T/A/M/000",
                rne = "B0158734212019",
                rc = "B0158734212019",
                iban = "TN59 0800 6000 0123 4567 8947",
                footerText = "Néoflux Solutions SARL · Capital 150 000 TND · RNE B0158734212019 · BIAT Les Berges du Lac · RIB 08 006 0000123456789 47",
                piedDePageTexte = "Néoflux Solutions SARL · Capital 150 000 TND · RNE B0158734212019 · BIAT Les Berges du Lac · RIB 08 006 0000123456789 47",
                enteteText = "{{entreprise.nom}} · {{entreprise.adresse}}, {{entreprise.ville}}\nTél : {{entreprise.telephone}} · MF : {{entreprise.matricule_fiscal}}",
                enteteTexte = "{{entreprise.nom}} · {{entreprise.adresse}}, {{entreprise.ville}}\nTél : {{entreprise.telephone}} · MF : {{entreprise.matricule_fiscal}}",
                signatureUrl = DataUri("signature.png"),
                sigImageUrl = DataUri("signature.png"),
                cachetUrl = DataUri("cachet.png"),
                signatureActive = true,
                hashActive = true,
                langue = "fr",
                arrondi = "3",
                options = new
                {
                    showLogo = true, showSignature = true, showTimbre = true, showMentionArrete = true, showIban = true, showQrCode = true,
                    showAdresseFactu = true, showAdresseLivraison = false, showPhotosArticles = false, showSolde = true, showDetailTaxes = true,
                    remiseSousTotal = true, cgvSurFacture = true, mentionExoneration = true, mentionPenalite = true
                }
            },
            conditions = new
            {
                delaiPaiement = 30, devise = "TND", langue = "fr", penaliteRetard = 1.5,
                cgv = "Toute facture non réglée à son échéance sera majorée de plein droit d'une pénalité de retard de 1,5 % par mois.\n\nRèglement par virement sur le compte BIAT n° 08 006 0000123456789 47 ou par chèque à l'ordre de Néoflux Solutions SARL.\n\nEn cas de litige, les tribunaux de Tunis sont seuls compétents.",
                options = new { penaliteRetard = true, cgvSurFacture = true, mentionExoneration = true }
            }
        };
        var json = JsonSerializer.Serialize(personnalisation);
        var perso = await _db.Personnalisations.FirstOrDefaultAsync(p => p.EntrepriseId == _eid);
        if (perso is null) _db.Personnalisations.Add(Personnalisation.Creer(_eid, json)); else perso.MettreAJour(json);

        var profils = new (Utilisateur U, string Prenom, string Nom, string Tel, string Poste, string Dept)[]
        {
            (_responsable, "Leila", "Ben Salem", "+216 29 888 777", "Gérante", "Direction générale"),
            (_admin, "Karim", "Ayari", "+216 20 990 011", "Administrateur systèmes", "IT"),
            (_financier, "Mouna", "Khelifi", "+216 98 230 455", "Responsable administratif et financier", "Finance"),
            (_ventes, "Sami", "Bouaziz", "+216 52 671 309", "Chargé d'affaires", "Commercial"),
        };
        foreach (var p in profils)
        {
            p.U.MettreAJourProfil(p.Prenom, p.Nom, p.Tel, p.Poste, p.Dept);
            Set(p.U, "CreeLe", new DateTime(2025, 3, 12, 10, 0, 0, DateTimeKind.Utc));
        }

        var hasher = _sp.GetRequiredService<IPasswordHasher>();
        const string emailYoussef = "youssef.gharbi@neoflux.tn";
        _commercial2 = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == emailYoussef)
            ?? Utilisateur.Creer("Youssef", "Gharbi", emailYoussef, hasher.Hacher("Demo@2026!"), RoleUtilisateur.CollaborateurVentes, _eid);
        if (_db.Entry(_commercial2).State == EntityState.Detached) _db.Utilisateurs.Add(_commercial2);
        _commercial2.MettreAJourProfil("Youssef", "Gharbi", "+216 55 408 912", "Commercial terrain", "Commercial");
        const string emailRim = "rim.chaabane@neoflux.tn";
        var rim = await _db.Utilisateurs.FirstOrDefaultAsync(u => u.Email == emailRim)
            ?? Utilisateur.Creer("Rim", "Chaabane", emailRim, hasher.Hacher("Demo@2026!"), RoleUtilisateur.ResponsableFinancier, _eid);
        if (_db.Entry(rim).State == EntityState.Detached) _db.Utilisateurs.Add(rim);
        rim.MettreAJourProfil("Rim", "Chaabane", "+216 97 115 620", "Comptable", "Finance");

        var abonnement = await _db.AbonnementsSaas.FirstOrDefaultAsync(a => a.EntrepriseId == _eid);
        if (abonnement is null)
            _db.AbonnementsSaas.Add(AbonnementSaas.Creer(_eid, "Business", 249m, 15, 500, _now.AddDays(43)));

        var exercices = new[] { ExerciceComptable.Creer(_eid, 2025), ExerciceComptable.Creer(_eid, 2026) };
        _db.ExercicesComptables.AddRange(exercices);
        await _db.SaveChangesAsync();
        exercices[0].Cloturer();
        await StampAsync(new DateTime(2026, 3, 31, 16, 0, 0, DateTimeKind.Utc));
        _db.ParametresPlateforme.RemoveRange(_db.ParametresPlateforme.Where(p => p.Cle == CleMarqueur + _eid));
        _db.ParametresPlateforme.Add(ParametrePlateforme.Creer(CleMarqueur + _eid,
            JsonSerializer.Serialize(new { genereLe = _now, periode = $"{_debut:yyyy-MM-dd}/{_now:yyyy-MM-dd}" }),
            "Json", "Historique de démonstration généré pour la vidéo"));
        await StampAsync(_debut.AddDays(-20));
    }

    // ------------------------------------------------------------------ catalogue

    private async Task CatalogueAsync()
    {
        var logiciels = CategorieProduit.Creer(_eid, "Logiciels & licences", "Licences ERP et abonnements logiciels");
        var services = CategorieProduit.Creer(_eid, "Services IT", "Développement, intégration et maintenance");
        var materiel = CategorieProduit.Creer(_eid, "Matériel informatique", "Postes, réseau et équipements");
        var formation = CategorieProduit.Creer(_eid, "Formation", "Sessions de formation utilisateurs");
        _db.Categories.AddRange(logiciels, services, materiel, formation);

        void P(string code, string libelle, decimal prix, decimal tva, TypeProduit type, CategorieProduit cat, string desc, string unite)
            => _produits.Add(Produit.Creer(_eid, code, libelle, prix, tva, type, cat.Id, desc, unite));

        P("LIC-ERP-VTE", "Licence ERP Néoflux - module Ventes (annuelle)", 2400m, 19m, TypeProduit.Service, logiciels, "Licence annuelle, mises à jour incluses", "AN");
        P("LIC-ERP-CPT", "Licence ERP Néoflux - module Comptabilité (annuelle)", 2900m, 19m, TypeProduit.Service, logiciels, "Licence annuelle, mises à jour incluses", "AN");
        P("LIC-M365", "Licence Microsoft 365 Business Standard", 310m, 19m, TypeProduit.Service, logiciels, "Abonnement annuel par utilisateur", "AN");
        P("MAINT-APP", "Maintenance applicative mensuelle", 650m, 19m, TypeProduit.Service, services, "Correctifs, évolutions mineures et assistance", "MOIS");
        P("DEV-JH", "Développement spécifique (jour-homme)", 480m, 19m, TypeProduit.Service, services, "Ingénieur développement confirmé", "JOUR");
        P("AUDIT-SI", "Audit de sécurité du système d'information", 3200m, 19m, TypeProduit.Service, services, "Audit, rapport et plan de remédiation", "FORFAIT");
        P("MIGR-DATA", "Migration et reprise de données", 1800m, 19m, TypeProduit.Service, services, "Reprise de l'historique depuis l'ancien logiciel", "FORFAIT");
        P("HELPDESK", "Support helpdesk utilisateurs", 320m, 19m, TypeProduit.Service, services, "Assistance téléphonique et à distance", "MOIS");
        P("VPS-CLOUD", "Hébergement cloud VPS managé", 180m, 19m, TypeProduit.Service, services, "Serveur virtuel, supervision et sauvegardes", "MOIS");
        P("FORM-JOUR", "Formation utilisateurs (journée)", 900m, 19m, TypeProduit.Service, formation, "Session sur site, 10 participants maximum", "JOUR");
        P("PC-PRODESK", "Poste de travail HP ProDesk 400 G9", 2350m, 19m, TypeProduit.Produit, materiel, "Intel Core i5, 16 Go RAM, SSD 512 Go", "PIECE");
        P("ECRAN-24", "Écran Dell 24 pouces P2423", 520m, 19m, TypeProduit.Produit, materiel, "Full HD, IPS, réglable en hauteur", "PIECE");
        P("SWITCH-24", "Switch réseau manageable 24 ports", 1150m, 19m, TypeProduit.Produit, materiel, "Gigabit, 4 ports SFP", "PIECE");
        P("ONDULEUR", "Onduleur APC 1500 VA", 690m, 19m, TypeProduit.Produit, materiel, "Line interactive, 8 prises", "PIECE");
        P("NAS-4TO", "Serveur de sauvegarde NAS 4 To", 1450m, 19m, TypeProduit.Produit, materiel, "2 baies, RAID 1, sauvegarde automatisée", "PIECE");
        P("CABLAGE", "Câblage réseau (point de connexion)", 85m, 19m, TypeProduit.Service, services, "Fourniture et pose, certification Cat6", "POINT");
        P("IMPR-LASER", "Imprimante laser multifonction", 780m, 19m, TypeProduit.Produit, materiel, "Recto-verso, réseau, scanner", "PIECE");
        P("GUIDE-TEIF", "Guide pratique de la facture électronique", 45m, 7m, TypeProduit.Produit, formation, "Ouvrage imprimé, édition 2026", "PIECE");
        _db.Produits.AddRange(_produits);
        await StampAsync(_debut.AddDays(-15));
    }

    private Produit Prod(string code) => _produits.First(p => p.Code == code);

    // ------------------------------------------------------------------ tiers

    private async Task TiersAsync()
    {
        void C(string nom, string email, TypeClient type, string? mf, string adresse, string ville, string cp, string tel,
            int rmin, int rmax, int delai, bool rs, int poids, params string[] familles)
        {
            var c = Client.Creer(_eid, nom, email, type, mf, adresse, ville, cp, tel);
            _db.Clients.Add(c);
            _clients.Add(new ClientDemo(c, rmin, rmax, delai, rs, familles, poids));
        }

        C("Sahel Logistique SARL", "finance@sahel-logistique.tn", TypeClient.B2B, "1325478LAM000", "Route de ceinture, ZI Sidi Abdelhamid", "Sousse", "4061", "+216 73 382 140", -3, 5, 30, false, 6, "erp", "materiel");
        C("Dar El Fanni Industries SA", "comptabilite@darelfanni.tn", TypeClient.B2B, "0874521FAM000", "Route de Gabès km 4", "Sfax", "3083", "+216 74 467 310", 5, 25, 45, false, 6, "erp", "services");
        C("Clinique Les Jasmins", "daf@clinique-lesjasmins.tn", TypeClient.B2B, "1199834JAM000", "12 Rue du Lac Michigan, Les Berges du Lac", "Tunis", "1053", "+216 71 962 500", 0, 8, 30, false, 5, "materiel", "services");
        C("Hôtel Médina Palace", "achats@medinapalace.tn", TypeClient.B2B, "0567342MAM000", "Zone touristique Yasmine", "Hammamet", "8057", "+216 72 241 900", 25, 60, 30, false, 5, "services", "materiel");
        C("Atlas Agro Export SARL", "compta@atlas-agro.tn", TypeClient.B2B, "1402876AAM000", "Avenue Habib Thameur", "Nabeul", "8000", "+216 72 285 330", 3, 20, 30, false, 4, "erp", "formation");
        C("Bizerte Marine Services", "contact@bizertemarine.tn", TypeClient.B2B, "1287453BAM000", "Port de pêche, quai nord", "Bizerte", "7000", "+216 72 431 220", 15, 45, 30, false, 3, "services", "materiel");
        C("Groupe Oliveraie du Centre SA", "tresorerie@oliveraie-centre.tn", TypeClient.B2B, "0912384OAM000", "Route de Sousse km 3", "Kairouan", "3100", "+216 77 233 410", 40, 85, 60, true, 4, "erp", "services");
        C("Cabinet Ben Romdhane Experts-Comptables", "cabinet@benromdhane-ec.tn", TypeClient.B2B, "1534201CNP000", "Immeuble Les Jardins, Ennasr 2", "Ariana", "2037", "+216 70 836 115", -2, 4, 15, false, 5, "erp", "formation");
        C("TechnoPack Emballages SARL", "finance@technopack.tn", TypeClient.B2B, "1478965TAM000", "Zone industrielle Mghira 3", "Ben Arous", "2082", "+216 79 409 820", 8, 30, 45, false, 4, "materiel", "services");
        C("Pharmacie Ennasr", "pharmacie.ennasr@gmail.com", TypeClient.B2B, "1609324PNP000", "Avenue Hédi Nouira, Ennasr 1", "Ariana", "2037", "+216 71 816 440", 0, 6, 30, false, 2, "materiel");
        C("Agence Régionale de Développement Numérique du Sahel", "marches@ardn-sahel.tn", TypeClient.B2G, "1180765GNN000", "Boulevard de l'Environnement", "Monastir", "5000", "+216 73 461 900", 30, 75, 60, true, 3, "erp", "formation", "services");
        C("Ines Mansouri", "ines.mansouri@gmail.com", TypeClient.B2C, "08745213", "Résidence Ennasr, bloc B", "Ariana", "2037", "+216 55 066 778", 0, 3, 7, false, 1, "materiel");
        C("Mehdi Jaziri", "mehdi.jaziri@outlook.com", TypeClient.B2C, "07651209", "Cité El Khadra", "Tunis", "1003", "+216 98 412 330", 0, 5, 7, false, 1, "materiel");
        C("Sarra Hamdi", "sarra.hamdi@yahoo.fr", TypeClient.B2C, "09123478", "Rue de Marseille", "Sfax", "3000", "+216 22 715 604", 0, 2, 7, false, 1, "formation", "materiel");

        void F(string nom, string mf, string adresse, string iban, string email, string tel)
            => _fournisseurs.Add(Fournisseur.Creer(_eid, nom, mf, adresse, iban, email, tel));
        F("InfoDistrib Tunisie SARL", "1293847KAM000", "Zone industrielle Charguia II, Tunis", "TN59 1000 6035 1835 9847 8831", "ventes@infodistrib.tn", "+216 71 771 230");
        F("Bureau Plus Fournitures", "1102938BAM000", "45 Avenue de Carthage, Tunis", "TN59 0500 0000 1234 5678 9012", "commandes@bureauplus.tn", "+216 71 335 480");
        F("Cloud Maghreb Hosting", "1456021HAM000", "Technopôle El Ghazala, Ariana", "TN59 0810 0000 6612 3398 4421", "facturation@cloudmaghreb.tn", "+216 70 250 600");
        F("Station Lac Carburants", "0981234SNM000", "Route de La Marsa, Tunis", "TN59 1100 0000 4512 7789 3360", "station.lac@gmail.com", "+216 71 860 115");
        F("Restaurant Le Golfe", "1311208RNM000", "Rue du Lac Léman, Les Berges du Lac", "TN59 0400 0000 3311 2208 1190", "legolfe.lac@gmail.com", "+216 71 963 222");
        F("Imprimerie Al Wafa", "1210457IAM000", "12 Rue de Marseille, Tunis", "TN59 0700 0000 1210 4570 2210", "contact@alwafa-print.tn", "+216 71 254 890");
        F("Clim Services Plus", "1388765CAM000", "Avenue Habib Bourguiba, Le Kram", "TN59 0300 0000 1388 7650 4410", "sav@climservices.tn", "+216 71 730 455");
        F("TechnoNet Câblage", "1477230TAM000", "Rue Ibn Khaldoun, Ben Arous", "TN59 1200 0000 1477 2300 8870", "devis@technonet.tn", "+216 79 386 512");
        F("Librairie Al Kitab", "0874512LNM000", "Avenue de France, Tunis", "TN59 0200 0000 0874 5120 1180", "alkitab.librairie@gmail.com", "+216 71 245 330");
        F("Taxi Express Tunis", "0765390TNP000", "Tunis", "TN59 0100 0000 0765 3900 2230", "taxiexpress.tn@gmail.com", "+216 98 100 200");
        F("Société Immobilière Les Berges", "1012345SAM000", "Rue du Lac Biwa, Les Berges du Lac, Tunis", "TN59 0800 6000 0101 2345 6789", "gestion@sim-berges.tn", "+216 71 960 100");
        F("Assurances Al Amen Conseil", "1055667AAM000", "Avenue Mohamed V, Tunis", "TN59 0400 0000 1055 6670 3321", "contrats@alamen-conseil.tn", "+216 71 340 800");
        F("Cabinet Juridique Trabelsi & Associés", "1622013JNP000", "Rue d'Angleterre, Tunis", "TN59 0500 0000 1622 0130 5541", "contact@trabelsi-avocats.tn", "+216 71 323 777");
        _db.Fournisseurs.AddRange(_fournisseurs);
        await StampAsync(_debut.AddDays(-10));
    }

    private Fournisseur Four(string debut) => _fournisseurs.First(f => f.Nom.StartsWith(debut, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ ventes

    private List<(Produit P, decimal Q, decimal Remise)> LignesPour(ClientDemo c)
    {
        var famille = Pick(c.Familles);
        var lignes = new List<(Produit, decimal, decimal)>();
        var b2c = c.Client.TypeClient == TypeClient.B2C;
        switch (famille)
        {
            case "erp":
                lignes.Add((Prod(_rng.Next(2) == 0 ? "LIC-ERP-VTE" : "LIC-ERP-CPT"), 1, 0));
                if (_rng.Next(2) == 0) lignes.Add((Prod("MAINT-APP"), _rng.Next(1, 4), 0));
                if (_rng.Next(3) == 0) lignes.Add((Prod("MIGR-DATA"), 1, _rng.Next(2) == 0 ? 10 : 0));
                break;
            case "services":
                lignes.Add((Prod(Pick(new[] { "DEV-JH", "MAINT-APP", "HELPDESK", "VPS-CLOUD" })), Pick(new[] { 1m, 2m, 3m, 5m }), 0));
                if (_rng.Next(3) == 0) lignes.Add((Prod("AUDIT-SI"), 1, 5));
                if (_rng.Next(2) == 0) lignes.Add((Prod("HELPDESK"), 1, 0));
                break;
            case "materiel":
                if (b2c)
                {
                    lignes.Add((Prod(Pick(new[] { "ECRAN-24", "IMPR-LASER", "ONDULEUR" })), 1, 0));
                    if (_rng.Next(2) == 0) lignes.Add((Prod("GUIDE-TEIF"), 1, 0));
                }
                else
                {
                    lignes.Add((Prod("PC-PRODESK"), _rng.Next(2, 7), _rng.Next(3) == 0 ? 5 : 0));
                    lignes.Add((Prod("ECRAN-24"), _rng.Next(2, 7), 0));
                    if (_rng.Next(2) == 0) lignes.Add((Prod(Pick(new[] { "SWITCH-24", "NAS-4TO", "ONDULEUR" })), 1, 0));
                    if (_rng.Next(2) == 0) lignes.Add((Prod("CABLAGE"), _rng.Next(8, 25), 0));
                }
                break;
            default:
                lignes.Add((Prod("FORM-JOUR"), b2c ? 1 : _rng.Next(1, 4), 0));
                if (_rng.Next(2) == 0) lignes.Add((Prod("GUIDE-TEIF"), b2c ? 1 : _rng.Next(5, 15), 0));
                break;
        }
        return lignes;
    }

    private ClientDemo ClientAuHasard()
    {
        var total = _clients.Sum(c => c.Poids);
        var x = _rng.Next(total);
        foreach (var c in _clients)
        {
            if (x < c.Poids) return c;
            x -= c.Poids;
        }
        return _clients[0];
    }

    private async Task<Facture> CreerFactureAsync(ClientDemo c, TypeFacture type, DateTime emission, DateTime echeance,
        List<(Produit P, decimal Q, decimal Remise)> lignes, Utilisateur auteur, Facture? origine = null, string? notes = null)
    {
        var mode = c.Client.TypeClient == TypeClient.B2C ? Pick(new[] { ModePaiement.CarteBancaire, ModePaiement.Especes })
            : c.Rs ? ModePaiement.Virement : Pick(new[] { ModePaiement.Virement, ModePaiement.Virement, ModePaiement.Cheque, ModePaiement.Traite });
        var conditions = c.Delai <= 7 ? "Paiement à réception" : $"Paiement à {c.Delai} jours fin de mois";
        var f = Facture.Creer(_eid, c.Client.Id, auteur.Id, Numero(emission), type, mode, _now.AddYears(2),
            type == TypeFacture.Avoir ? $"Avoir sur {origine!.Numero}" : $"BC-{c.Client.Nom[..3].ToUpperInvariant()}-{emission:yyMMdd}",
            notes, conditions, "TND", c.Rs && type == TypeFacture.Facture, c.Rs ? "RS-1.5" : null, c.Rs ? 1.5m : 0, origine?.Id);
        var ordre = 1;
        foreach (var l in lignes)
            f.AjouterLigne(LigneFacture.Creer(f.Id, ordre++, l.P.Libelle, l.Q, l.P.PrixUnitaire, l.P.TauxTva, l.P.Id, l.P.Description, l.P.Unite, l.Remise));
        _db.Factures.Add(f);
        Set(f, nameof(Facture.DateEcheance), J(echeance, 0));
        await StampAsync(emission);
        Audit(emission, auteur, "POST", "factures", "/api/factures");
        _factures.Add(f);
        return f;
    }

    /// <summary>Validation → XML TEIF → conformité → signature → envoi TTN → réponse.</summary>
    private async Task<bool> CircuitTeifAsync(Facture f, DateTime emission, Utilisateur valideur, string reponse, bool attendreReponse = true)
    {
        var factureService = _sp.GetRequiredService<IFactureService>();
        var teif = _sp.GetRequiredService<ITeifService>();
        var signature = _sp.GetRequiredService<ISignatureService>();
        var ttn = _sp.GetRequiredService<IEchangeTtnService>();

        var t = emission.AddMinutes(_rng.Next(20, 140));
        await factureService.ValiderAsync(f.Id, _eid, valideur.Id);
        await StampAsync(t);
        Audit(t, valideur, "POST", "factures", $"/api/factures/{f.Id}/valider");

        t = t.AddMinutes(3);
        await teif.GenererXmlAsync(f.Id, _eid);
        await teif.MarquerConformeAsync(f.Id, _eid, valideur.Id);
        await StampAsync(t);
        Audit(t, valideur, "POST", "teif", $"/api/teif/{f.Id}/generer-xml");

        t = t.AddMinutes(2);
        // Après une remise en brouillon, l'ancienne signature porte sur un XML qui n'existe plus.
        _db.Signatures.RemoveRange(await _db.Signatures.Where(s => s.FactureId == f.Id).ToListAsync());
        await _db.SaveChangesAsync();
        await signature.DemanderSignatureAsync(f.Id, _eid, _responsable.Id);
        await StampAsync(t);
        Audit(t, _responsable, "POST", "signatures", "/api/signatures/demander");

        t = t.AddMinutes(1);
        var echange = await ttn.EnvoyerAsync(f.Id, _eid, valideur.Id);
        await StampAsync(t);
        Audit(t, valideur, "POST", "ttn", "/api/ttn/envoyer");
        if (!attendreReponse) return false;

        t = t.AddMinutes(_rng.Next(25, 600));
        await ttn.SimulerReponseAsync(echange.Id, reponse);
        await StampAsync(t);
        return reponse == "accepte";
    }

    private async Task PayerAsync(Facture f, DateTime date, decimal montant, Utilisateur qui)
    {
        var paiements = _sp.GetRequiredService<IPaiementService>();
        var banque = Pick(new[] { "BIAT", "Amen Bank", "Attijari Bank", "BH Bank", "STB" });
        var reference = f.ModePaiement switch
        {
            ModePaiement.Cheque => $"CHQ-{_rng.Next(1000000, 9999999)}",
            ModePaiement.Traite => $"LC-{_rng.Next(100000, 999999)}",
            ModePaiement.Especes => $"ESP-{date:yyMMdd}-{_rng.Next(10, 99)}",
            ModePaiement.CarteBancaire => $"TPE-{_rng.Next(100000, 999999)}",
            _ => $"VIR-{date:yyMMdd}-{_rng.Next(1000, 9999)}"
        };
        await paiements.EnregistrerAsync(_eid, qui.Id,
            new EnregistrerPaiementRequest(f.Id, montant, f.ModePaiement, J(date, 10), reference, banque, "Règlement reçu"));
        await StampAsync(J(date, _rng.Next(9, 17), _rng.Next(0, 59)));
        Audit(J(date, 11), qui, "POST", "paiements", "/api/paiements");
    }

    private async Task VentesAsync()
    {
        var auteurs = new[] { _ventes, _ventes, _commercial2, _responsable };
        var jour = Ouvre(_debut.AddDays(2));
        var rejetsRecents = 0;
        var annulees = 0;
        while (jour < _now.Date)
        {
            var ageJours = (_now.Date - jour).TotalDays;
            var parJour = _rng.NextDouble() < 0.72 ? 1 : 0;
            if (_rng.NextDouble() < 0.12) parJour++;
            for (var k = 0; k < parJour; k++)
            {
                var c = ClientAuHasard();
                var emission = J(jour, _rng.Next(8, 12), _rng.Next(0, 59));
                var echeance = emission.Date.AddDays(c.Delai);
                var auteur = Pick(auteurs);
                var facture = await CreerFactureAsync(c, TypeFacture.Facture, emission, echeance, LignesPour(c), auteur);

                if (ageJours <= 2 && _rng.NextDouble() < 0.6) continue; // brouillon en cours
                if (ageJours <= 4 && _rng.NextDouble() < 0.5)
                {
                    var fs = _sp.GetRequiredService<IFactureService>();
                    await fs.ValiderAsync(facture.Id, _eid, auteur.Id);
                    await StampAsync(emission.AddHours(1));
                    continue; // validée, XML pas encore généré
                }
                if (annulees < 2 && ageJours > 40 && _rng.NextDouble() < 0.05)
                {
                    var fs = _sp.GetRequiredService<IFactureService>();
                    await fs.ValiderAsync(facture.Id, _eid, auteur.Id);
                    await StampAsync(emission.AddHours(1));
                    await fs.AnnulerAsync(facture.Id, _eid, _responsable.Id, new AnnulerFactureRequest("Commande annulée par le client avant livraison"));
                    await StampAsync(emission.AddDays(1).AddHours(2));
                    annulees++;
                    continue;
                }

                var rejet = _rng.NextDouble() < 0.06;
                if (ageJours <= 6 && !rejet && _rng.NextDouble() < 0.3)
                {
                    await CircuitTeifAsync(facture, emission, auteur, "accepte", attendreReponse: false);
                    continue; // transmise, en attente de réponse TTN
                }

                var acceptee = await CircuitTeifAsync(facture, emission, auteur, rejet ? "rejete" : "accepte");
                if (!acceptee)
                {
                    if (ageJours <= 12 || rejetsRecents == 0 && ageJours <= 30) { rejetsRecents++; continue; }
                    // Correction après rejet : remise en brouillon puis nouveau circuit complet.
                    var fs = _sp.GetRequiredService<IFactureService>();
                    await fs.RemettreBrouillonAsync(facture.Id, _eid, _financier.Id);
                    await StampAsync(emission.AddDays(1).AddHours(3));
                    acceptee = await CircuitTeifAsync(facture, emission.AddDays(1).AddHours(4), _financier, "accepte");
                }

                // Encaissement selon le comportement de paiement du client.
                var retard = _rng.Next(c.RetardMin, c.RetardMax + 1);
                var datePaiement = Ouvre(echeance.AddDays(retard));
                if (datePaiement < emission.Date.AddDays(1)) datePaiement = Ouvre(emission.Date.AddDays(1));
                var net = facture.NetAPayer;
                if (datePaiement < _now.Date)
                {
                    if (net > 6000 && _rng.NextDouble() < 0.45)
                    {
                        var acompte = Math.Round(net * 0.4m, 3);
                        var dateAcompte = Ouvre(emission.Date.AddDays(Math.Max(3, retard / 3 + 5)));
                        if (dateAcompte >= datePaiement) dateAcompte = datePaiement.AddDays(-3);
                        await PayerAsync(facture, dateAcompte, acompte, _financier);
                        await PayerAsync(facture, datePaiement, net - acompte, _financier);
                    }
                    else
                    {
                        await PayerAsync(facture, datePaiement, net, _financier);
                    }
                }
                else if (echeance < _now.Date && net > 3000 && _rng.NextDouble() < 0.35)
                {
                    // Client en retard qui a versé un acompte.
                    await PayerAsync(facture, Ouvre(echeance.AddDays(-2)), Math.Round(net * 0.3m, 3), _financier);
                }
            }
            jour = Ouvre(jour.AddDays(1));
        }

        await FacturesEnCoursAsync();
        await AvoirsEtDevisAsync();
        _log.LogInformation("Ventes générées : {Count} documents.", _factures.Count);
    }

    /// <summary>Factures du jour à chaque étape du cycle, pour montrer le parcours complet en démonstration.</summary>
    private async Task FacturesEnCoursAsync()
    {
        var fs = _sp.GetRequiredService<IFactureService>();
        var teif = _sp.GetRequiredService<ITeifService>();
        var aujourdHui = _now.AddHours(-5);
        var hier = J(Ouvre(_now.Date.AddDays(-1)) > _now.Date ? _now.Date.AddDays(-3) : _now.Date.AddDays(-1), 10, 15);
        ClientDemo Cl(string debut) => _clients.First(c => c.Client.Nom.StartsWith(debut));

        // Brouillons en cours de saisie
        await CreerFactureAsync(Cl("Clinique"), TypeFacture.Facture, aujourdHui, aujourdHui.Date.AddDays(30), LignesPour(Cl("Clinique")), _ventes);
        await CreerFactureAsync(Cl("Pharmacie"), TypeFacture.Facture, aujourdHui.AddMinutes(40), aujourdHui.Date.AddDays(30), LignesPour(Cl("Pharmacie")), _commercial2);

        // Validées, XML TEIF à générer
        foreach (var nom in new[] { "Sahel", "Cabinet" })
        {
            var f = await CreerFactureAsync(Cl(nom), TypeFacture.Facture, hier, hier.Date.AddDays(Cl(nom).Delai), LignesPour(Cl(nom)), _ventes);
            await fs.ValiderAsync(f.Id, _eid, _ventes.Id);
            await StampAsync(hier.AddHours(2));
        }

        // Conforme TEIF, en attente de signature
        var conforme = await CreerFactureAsync(Cl("Atlas"), TypeFacture.Facture, hier.AddHours(1), hier.Date.AddDays(30), LignesPour(Cl("Atlas")), _ventes);
        await fs.ValiderAsync(conforme.Id, _eid, _ventes.Id);
        await teif.GenererXmlAsync(conforme.Id, _eid);
        await teif.MarquerConformeAsync(conforme.Id, _eid, _ventes.Id);
        await StampAsync(hier.AddHours(3));

        // Transmises à TTN, réponse attendue
        foreach (var nom in new[] { "Dar El Fanni", "TechnoPack" })
        {
            var f = await CreerFactureAsync(Cl(nom), TypeFacture.Facture, hier.AddHours(2), hier.Date.AddDays(Cl(nom).Delai), LignesPour(Cl(nom)), _commercial2);
            await CircuitTeifAsync(f, hier.AddHours(2), _commercial2, "accepte", attendreReponse: false);
        }

        // Rejet TTN récent, à corriger
        var rejet = await CreerFactureAsync(Cl("Bizerte"), TypeFacture.Facture, hier.AddHours(-1), hier.Date.AddDays(30), LignesPour(Cl("Bizerte")), _ventes);
        await CircuitTeifAsync(rejet, hier.AddHours(-1), _ventes, "rejete");
    }

    private async Task AvoirsEtDevisAsync()
    {
        var candidates = _factures.Where(f => f.TypeFacture == TypeFacture.Facture
                && (f.Statut == StatutFacture.Payee || f.Statut == StatutFacture.Acceptee)
                && f.DateEmission < _now.AddDays(-20))
            .OrderBy(_ => _rng.Next()).Take(5).ToList();
        var motifs = new[]
        {
            "Retour d'un écran défectueux sous garantie",
            "Remise commerciale accordée après réclamation",
            "Jour de formation non consommé",
            "Erreur de quantité sur la livraison",
            "Geste commercial sur la maintenance du trimestre"
        };
        var i = 0;
        foreach (var origine in candidates)
        {
            var c = _clients.First(x => x.Client.Id == origine.ClientId);
            var ligne = origine.Lignes.OrderBy(l => l.MontantHt).First();
            var produit = _produits.First(p => p.Id == ligne.ProduitId);
            var emission = J(Ouvre(origine.DateEmission.Date.AddDays(_rng.Next(6, 18))), 14, 10);
            if (emission > _now.AddDays(-2)) emission = _now.AddDays(-3);
            var avoir = await CreerFactureAsync(c, TypeFacture.Avoir, emission, emission.Date,
                new() { (produit, 1, ligne.TauxRemise) }, _financier, origine, motifs[i++ % motifs.Length]);
            await CircuitTeifAsync(avoir, emission, _financier, "accepte");
        }

        // Devis et proformas (page Devis)
        for (var d = 0; d < 12; d++)
        {
            var c = ClientAuHasard();
            var emission = J(Ouvre(_now.Date.AddDays(-_rng.Next(1, 75))), 15, _rng.Next(0, 59));
            var type = d < 9 ? TypeFacture.Devis : TypeFacture.Proforma;
            var devis = await CreerFactureAsync(c, type, emission, emission.Date.AddDays(30), LignesPour(c), Pick(new[] { _ventes, _commercial2 }),
                notes: type == TypeFacture.Devis ? "Offre valable 30 jours." : "Facture proforma pour engagement budgétaire.");
            if (_rng.NextDouble() < 0.55)
            {
                await _sp.GetRequiredService<IFactureService>().ValiderAsync(devis.Id, _eid, _ventes.Id);
                await StampAsync(emission.AddHours(2));
            }
        }
    }

    // ------------------------------------------------------------------ documents commerciaux

    private string LignesJson(Facture f) => JsonSerializer.Serialize(f.Lignes.OrderBy(l => l.Ordre)
        .Select(l => new DocumentsCommerciauxController.Ligne(l.Designation, l.Quantite, l.PrixUnitaire, l.TauxTva, l.Unite, l.Description, l.TauxRemise)).ToList());

    private readonly Dictionary<string, int> _numerosDocs = new();

    private string NumeroDoc(string type, DateTime date)
    {
        var prefixe = type switch { "bon_commande" => "BC", "bon_livraison" => "BL", _ => "DOC" };
        var cle = $"{prefixe}-{date.Year}";
        _numerosDocs[cle] = _numerosDocs.GetValueOrDefault(cle) + 1;
        return $"{cle}-{_numerosDocs[cle]:D4}";
    }

    private async Task<DocumentCommercial> DocAsync(string type, Guid clientId, Utilisateur auteur, DateTime date, DateTime? echeance,
        decimal ht, decimal tva, decimal ttc, string lignes, string statut, string? reference, string? notes, Guid? origine)
    {
        var doc = DocumentCommercial.Creer(_eid, clientId, auteur.Id, type, NumeroDoc(type, date), date, echeance, ht, tva, ttc, lignes, reference, notes, origine);
        _db.DocumentsCommerciaux.Add(doc);
        await StampAsync(date);
        doc.MettreAJour(statut, echeance, lignes, ht, tva, ttc, reference, notes);
        await StampAsync(date.AddDays(1).AddHours(2));
        Audit(date, auteur, "POST", "documents-commerciaux", "/api/documents-commerciaux");
        return doc;
    }

    private async Task DocumentsCommerciauxAsync()
    {
        // Chaînes complètes devis → bon de commande → bon de livraison → facture
        var facturees = _factures.Where(f => f.TypeFacture == TypeFacture.Facture && f.Statut is StatutFacture.Payee or StatutFacture.Acceptee or StatutFacture.PartiellemementPayee
                && _clients.First(c => c.Client.Id == f.ClientId).Client.TypeClient != TypeClient.B2C)
            .OrderBy(_ => _rng.Next()).Take(9).ToList();
        foreach (var f in facturees)
        {
            var lignes = LignesJson(f);
            var dCommande = J(Ouvre(f.DateEmission.Date.AddDays(-_rng.Next(10, 20))), 10);
            if (dCommande < _debut.AddDays(-20)) dCommande = J(_debut.AddDays(-15), 10);
            var bc = await DocAsync("bon_commande", f.ClientId, _ventes, dCommande, f.DateEcheance, f.TotalHt, f.TotalTva, f.TotalTtc, lignes, "Livré", f.Reference, "Commande confirmée par le client.", null);
            var bl = await DocAsync("bon_livraison", f.ClientId, _commercial2, J(f.DateEmission.Date.AddDays(-_rng.Next(1, 3)), 15), f.DateEcheance, f.TotalHt, f.TotalTva, f.TotalTtc, lignes, "Livré", f.Reference, "Livraison réceptionnée et signée.", bc.Id);
            bl.MarquerConverti(f.Id);
            await StampAsync(f.DateEmission);
        }

        // Commandes et livraisons en cours (pas encore facturées)
        var etats = new (string Type, string Statut, int AgeMin, int AgeMax, string Note)[]
        {
            ("bon_commande", "Reçu", 1, 3, "Commande reçue par email, à confirmer."),
            ("bon_commande", "Confirmé", 3, 8, "Livraison prévue la semaine prochaine."),
            ("bon_commande", "Confirmé", 4, 10, "Matériel commandé chez le distributeur."),
            ("bon_commande", "En cours", 6, 14, "Préparation et configuration des postes."),
            ("bon_commande", "Annulé", 25, 50, "Projet reporté par le client."),
            ("bon_livraison", "Préparé", 1, 3, "Colis prêts, enlèvement demain."),
            ("bon_livraison", "Expédié", 2, 5, "Livraison en cours par transporteur."),
            ("bon_livraison", "Litige", 10, 20, "Un écran livré endommagé, échange demandé."),
            ("bon_livraison", "Livré", 3, 7, "Livré, facture à émettre."),
        };
        foreach (var e in etats)
        {
            var c = _clients.Where(x => x.Client.TypeClient != TypeClient.B2C).OrderBy(_ => _rng.Next()).First();
            var lignes = LignesPour(c);
            var ht = lignes.Sum(l => Math.Round(l.Q * l.P.PrixUnitaire * (1 - l.Remise / 100m), 3));
            var tva = lignes.Sum(l => Math.Round(l.Q * l.P.PrixUnitaire * (1 - l.Remise / 100m) * l.P.TauxTva / 100m, 3));
            var json = JsonSerializer.Serialize(lignes.Select(l => new DocumentsCommerciauxController.Ligne(l.P.Libelle, l.Q, l.P.PrixUnitaire, l.P.TauxTva, l.P.Unite, l.P.Description, l.Remise)).ToList());
            var date = J(Ouvre(_now.Date.AddDays(-_rng.Next(e.AgeMin, e.AgeMax + 1))), 11);
            if (date > _now.AddHours(-2)) date = _now.AddHours(-3);
            Guid? origine = null;
            if (e.Type == "bon_livraison")
                origine = (await DocAsync("bon_commande", c.Client.Id, _ventes, date.AddDays(-4), date.AddDays(40), ht, tva, ht + tva, json, "Livré", null, "Commande confirmée.", null)).Id;
            await DocAsync(e.Type, c.Client.Id, Pick(new[] { _ventes, _commercial2 }), date, date.AddDays(40), ht, tva, ht + tva, json, e.Statut, null, e.Note, origine);
        }
    }

    // ------------------------------------------------------------------ achats

    private async Task FactureFournisseurAsync(Fournisseur f, string numero, DateTime date, int delai, (string D, decimal Q, decimal Pu, decimal Tva)[] lignes,
        decimal retenue, string? notes, bool payee, int retardPaiement = 0)
    {
        var json = JsonSerializer.Serialize(lignes.Select(l => new FacturesFournisseurController.LigneFactureFournisseur(l.D, l.Q, l.Pu, l.Tva)).ToList());
        var ht = lignes.Sum(l => Math.Round(l.Q * l.Pu, 3));
        var tva = lignes.Sum(l => Math.Round(Math.Round(l.Q * l.Pu, 3) * l.Tva / 100m, 3));
        var echeance = date.Date.AddDays(delai);
        var ff = FactureFournisseur.Creer(_eid, f.Id, _financier.Id, numero, J(date, 9), J(echeance, 0), ht, tva, retenue, json, $"Réf. {numero}", notes);
        _db.FacturesFournisseur.Add(ff);
        await StampAsync(J(date, 10, 30));
        ff.Valider();
        await StampAsync(J(date, 11));
        Audit(J(date, 10, 30), _financier, "POST", "factures-fournisseur", "/api/factures-fournisseur");
        var datePaiement = Ouvre(echeance.AddDays(retardPaiement));
        if (payee && datePaiement < _now.Date)
        {
            var montant = ff.NetAPayer - ff.MontantPaye;
            ff.EnregistrerPaiement(montant);
            _db.PaiementsFournisseur.Add(PaiementFournisseur.Creer(ff.Id, _eid, _financier.Id, montant, ModePaiement.Virement, J(datePaiement, 10), $"VIR-F-{datePaiement:yyMMdd}-{_rng.Next(100, 999)}", "Virement fournisseur"));
            await StampAsync(J(datePaiement, 10, 15));
            Audit(J(datePaiement, 10, 15), _financier, "POST", "factures-fournisseur", $"/api/factures-fournisseur/{ff.Id}/paiements");
        }
    }

    private async Task AchatsAsync()
    {
        var mois = Enumerable.Range(0, 7).Select(i => new DateTime(_debut.Year, _debut.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i))
            .Where(m => m <= _now).ToList();
        var n = 1;
        foreach (var m in mois)
        {
            var debutMois = Ouvre(m.AddDays(1));
            if (debutMois >= _debut && debutMois < _now.Date)
            {
                await FactureFournisseurAsync(Four("Société Immobilière"), $"LOY-{m:yyyyMM}", debutMois, 5,
                    new[] { ("Loyer bureaux Les Berges du Lac - 180 m²", 1m, 3400m, 19m) }, 0, "Loyer mensuel", true, 0);
                await FactureFournisseurAsync(Four("Cloud Maghreb"), $"CMH-{m:yyMM}-{400 + n}", Ouvre(m.AddDays(3)), 15,
                    new[] { ("Hébergement VPS 8 vCPU - mensuel", 1m, 420m, 19m), ("Sauvegarde externalisée 500 Go", 1m, 95m, 19m), ("Licences VPS clients revendues", 6m, 110m, 19m) }, 1.5m, null, true, _rng.Next(0, 6));
            }
            var achatMateriel = Ouvre(m.AddDays(_rng.Next(8, 20)));
            if (achatMateriel >= _debut && achatMateriel < _now.Date)
            {
                var postes = _rng.Next(3, 9);
                await FactureFournisseurAsync(Four("InfoDistrib"), $"FID-{m:yyMM}{_rng.Next(100, 999)}", achatMateriel, 30,
                    new[] { ("Poste HP ProDesk 400 G9 (revente)", (decimal)postes, 1780m, 19m), ("Écran Dell P2423 (revente)", (decimal)postes, 395m, 19m), ("Switch 24 ports", 1m, 870m, 19m) },
                    1.5m, "Matériel pour projets clients", true, _rng.Next(-2, 12));
            }
            var autre = Ouvre(m.AddDays(_rng.Next(12, 26)));
            if (autre >= _debut && autre < _now.Date)
            {
                switch (n % 4)
                {
                    case 0:
                        await FactureFournisseurAsync(Four("Bureau Plus"), $"BP-{m:yyMM}-{_rng.Next(1000, 9999)}", autre, 30,
                            new[] { ("Ramettes papier A4 (carton de 5)", 4m, 62.5m, 19m), ("Toner laser HP 26A", 2m, 245m, 19m), ("Classeurs et fournitures diverses", 1m, 138m, 19m) }, 0, null, true, 3);
                        break;
                    case 1:
                        await FactureFournisseurAsync(Four("TechnoNet"), $"TNC-{m:yyMM}-{_rng.Next(100, 999)}", autre, 30,
                            new[] { ("Câble réseau Cat6 (bobine 305 m)", 2m, 310m, 19m), ("Prestation de câblage sous-traitée", 18m, 45m, 19m) }, 1.5m, "Projet câblage client", true, 10);
                        break;
                    case 2:
                        await FactureFournisseurAsync(Four("Cabinet Juridique"), $"HON-{m:yyMM}-{_rng.Next(10, 99)}", autre, 30,
                            new[] { ("Honoraires conseil juridique - contrats clients", 1m, 1200m, 19m) }, 5m, "Retenue à la source 5 % sur honoraires", true, 0);
                        break;
                    default:
                        await FactureFournisseurAsync(Four("Clim Services"), $"CSP-{m:yyMM}-{_rng.Next(100, 999)}", autre, 15,
                            new[] { ("Maintenance climatisation salle serveurs", 1m, 380m, 19m), ("Remplacement filtre et recharge gaz", 1m, 210m, 19m) }, 0, null, true, 0);
                        break;
                }
            }
            n++;
        }
        // Factures fournisseurs récentes non échues ou en retard
        await FactureFournisseurAsync(Four("Assurances"), $"ASS-{_now:yyyy}-0417", Ouvre(_now.Date.AddDays(-40)), 30,
            new[] { ("Assurance multirisque bureaux - semestre", 1m, 1850m, 0m) }, 0, "Échéance dépassée, relance reçue", false);
        await FactureFournisseurAsync(Four("Imprimerie"), $"IAW-{_now:yyMM}-208", Ouvre(_now.Date.AddDays(-9)), 30,
            new[] { ("Plaquettes commerciales A4", 300m, 1.35m, 19m), ("Kakémono salon IT", 2m, 165m, 19m) }, 0, "Salon des solutions numériques", false);
    }

    // ------------------------------------------------------------------ trésorerie

    private async Task<Transaction> TxAsync(DateTime date, string libelle, decimal montant, TypeTransaction type, string tiers, string categorie,
        string compte, StatutTransaction statut, StatutJustificatif? justificatif, string? description = null, decimal? tvaRecuperable = null, bool rapprochee = true)
    {
        var tx = Transaction.Creer(_eid, _financier.Id, J(date, 0), libelle, montant, type, "TND", tiers, categorie, description, compte,
            null, DocumentSource.Web, statut, justificatif);
        _db.Transactions.Add(tx);
        if (tvaRecuperable is > 0)
            tx.DefinirAnalytique(_financier.Id, categorie, JsonSerializer.Serialize(new[] { new { axe = "Centre de coût", valeur = "Siège", pourcentage = 100 } }),
                date.ToString("MMMM yyyy", new CultureInfo("fr-FR")), tvaRecuperable, 19m);
        if (rapprochee && date < _now.Date.AddDays(-5))
            tx.DefinirRapprochementBancaire(_financier.Id, JsonSerializer.Serialize(new { releve = $"BIAT-{date:yyyy-MM}", ligne = _rng.Next(1, 60), rapprocheLe = date.AddDays(3), statut = "Rapprochée" }));
        await StampAsync(J(date, _rng.Next(9, 18), _rng.Next(0, 59)));
        Audit(J(date, 12), _financier, "POST", "transactions", "/api/transactions");
        return tx;
    }

    private async Task TresorerieAsync()
    {
        var mois = Enumerable.Range(0, 7).Select(i => new DateTime(_debut.Year, _debut.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i)).ToList();
        foreach (var m in mois)
        {
            async Task Si(DateTime d, Func<DateTime, Task> action) { d = Ouvre(d); if (d >= _debut && d < _now.Date) await action(d); }

            await Si(m.AddDays(4), d => TxAsync(d, "Abonnement fibre optique 100 Mb/s + lignes fixes", 289.900m, TypeTransaction.Sortie, "Tunisie Telecom", "Télécommunications", "626000", StatutTransaction.Justifiee, StatutJustificatif.Facultatif, "Prélèvement mensuel", 46.287m));
            await Si(m.AddDays(9), d => TxAsync(d, "Forfaits mobiles équipe commerciale", 214.500m, TypeTransaction.Sortie, "Ooredoo Tunisie", "Télécommunications", "626000", StatutTransaction.Justifiee, StatutJustificatif.Facultatif, null, 34.248m));
            await Si(m.AddDays(27), d => TxAsync(d, "Frais de tenue de compte et commissions", 38.500m, TypeTransaction.Sortie, "BIAT", "Frais bancaires", "627000", StatutTransaction.Justifiee, StatutJustificatif.Facultatif));
            await Si(m.AddDays(14), d => TxAsync(d, "Carburant véhicule de service", Math.Round(120m + (decimal)_rng.NextDouble() * 90m, 3), TypeTransaction.Sortie, "Station Lac Carburants", "Déplacements", "625100",
                _rng.NextDouble() < 0.7 ? StatutTransaction.Justifiee : StatutTransaction.NonJustifiee, _rng.NextDouble() < 0.7 ? StatutJustificatif.Present : StatutJustificatif.Perdu, "Plein gasoil"));
            await Si(m.AddDays(17), d => TxAsync(d, "Déjeuner d'affaires avec client", Math.Round(95m + (decimal)_rng.NextDouble() * 160m, 3), TypeTransaction.Sortie, "Restaurant Le Golfe", "Réceptions", "625700",
                _rng.NextDouble() < 0.6 ? StatutTransaction.Justifiee : StatutTransaction.EnAttente, StatutJustificatif.Present));
            await Si(m.AddDays(15), d => TxAsync(d, $"Déclaration mensuelle TVA {m.AddMonths(-1):MM/yyyy}", Math.Round(2600m + (decimal)_rng.NextDouble() * 2400m, 3), TypeTransaction.Sortie, "Recette des finances Lac", "Impôts et taxes", "436700", StatutTransaction.Justifiee, StatutJustificatif.Present, "Télédéclaration et paiement en ligne"));
            if (m.Month % 3 == 1)
                await Si(m.AddDays(20), d => TxAsync(d, $"Cotisations CNSS trimestre {(m.AddMonths(-1).Month + 2) / 3}", 9850.400m, TypeTransaction.Sortie, "CNSS", "Charges sociales", "453000", StatutTransaction.Justifiee, StatutJustificatif.Present));
            if (m.Month % 2 == 0)
                await Si(m.AddDays(11), d => TxAsync(d, "Facture électricité bimestrielle", Math.Round(640m + (decimal)_rng.NextDouble() * 320m, 3), TypeTransaction.Sortie, "STEG", "Énergie", "606100", StatutTransaction.Justifiee, StatutJustificatif.Facultatif, null, 98.5m));
            await Si(m.AddDays(22), d => TxAsync(d, "Intérêts créditeurs compte à terme", Math.Round(85m + (decimal)_rng.NextDouble() * 40m, 3), TypeTransaction.Entree, "BIAT", "Produits financiers", "532000", StatutTransaction.Justifiee, StatutJustificatif.Facultatif));
        }

        // Mouvements bancaires récents non rapprochés et dépenses à justifier
        await TxAsync(Ouvre(_now.Date.AddDays(-4)), "Virement reçu non identifié", 1250m, TypeTransaction.Entree, "Inconnu", "À affecter", "532000", StatutTransaction.EnAttente, null, "En attente d'identification du client", rapprochee: false);
        await TxAsync(Ouvre(_now.Date.AddDays(-3)), "Prélèvement assurance véhicule", 412.600m, TypeTransaction.Sortie, "Assurances Al Amen Conseil", "Assurances", "532000", StatutTransaction.NonJustifiee, StatutJustificatif.Perdu, rapprochee: false);
        await TxAsync(Ouvre(_now.Date.AddDays(-2)), "Achat petit matériel informatique", 186.900m, TypeTransaction.Sortie, "InfoDistrib Tunisie SARL", "Petit équipement", "606300", StatutTransaction.NonJustifiee, null, "Souris, claviers et adaptateurs", rapprochee: false);
        await TxAsync(Ouvre(_now.Date.AddDays(-6)), "Remboursement sinistre dégât des eaux", 1430m, TypeTransaction.Entree, "Assurances Al Amen Conseil", "Produits exceptionnels", "532000", StatutTransaction.Justifiee, StatutJustificatif.Present, rapprochee: false);
        // Dépense inhabituelle pour la détection d'anomalies
        await TxAsync(Ouvre(_now.Date.AddDays(-12)), "Paiement carte - achat en ligne étranger", 4890m, TypeTransaction.Sortie, "Fournisseur étranger", "À contrôler", "607000", StatutTransaction.EnAttente, null, "Montant inhabituel, justificatif demandé", rapprochee: false);
    }

    // ------------------------------------------------------------------ scans mobile (OCR réel)

    private async Task ScansAsync()
    {
        var csv = Path.Combine(_assets, "justificatifs.csv");
        if (!File.Exists(csv)) { _log.LogWarning("Justificatifs absents, scans ignorés."); return; }
        var scan = _sp.GetRequiredService<IScanService>();
        var lignes = File.ReadAllLines(csv).Skip(1).Select(l => l.Split(';')).ToList();
        var idx = 0;
        foreach (var l in lignes)
        {
            var (fichier, fournisseur, mf, type, date, numero, ttc) = (l[0], l[1], l[2], l[3], DateTime.Parse(l[4], CultureInfo.InvariantCulture), l[5], l[8]);
            var quand = J(DateTime.SpecifyKind(date, DateTimeKind.Utc), 17, 20);
            if (quand < _debut || quand > _now) continue;
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_assets, "justificatifs", fichier));
            await using var ms = new MemoryStream(bytes);
            var form = new FormFile(ms, 0, bytes.Length, "file", $"IMG_{date:yyyyMMdd}_{1700 + idx}.jpg")
            { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
            ScanUploadResponseDto upload;
            try
            {
                upload = await scan.UploadAsync(_eid, _commercial2.Id, form, "facture");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Scan {Fichier} ignoré (service OCR indisponible ?)", fichier);
                continue;
            }
            await StampAsync(quand);
            Audit(quand, _commercial2, "POST", "scan", "/api/scan/upload");

            var four = _fournisseurs.FirstOrDefault(f => f.Nom == fournisseur);
            var champs = new List<ValidateScanFieldRequest>
            {
                new("document_number", numero), new("issue_date", date.ToString("dd/MM/yyyy")), new("supplier_name", fournisseur),
                new("client_name", "Néoflux Solutions SARL"), new("total_ttc", ttc), new("trade_sense", "achat"),
                new("payment_status", type == "ticket" ? "Payée" : "Non payée"),
            };
            var validation = await scan.ValidateAsync(_eid, _commercial2.Id,
                new ValidateScanRequest(upload.ScannedDocumentId, champs, upload.DocumentType, four?.Id, fournisseur, mf));
            await StampAsync(quand.AddMinutes(2));
            Audit(quand.AddMinutes(2), _commercial2, "POST", "scan", "/api/scan/validate");

            // Le service financier contrôle et valide la plupart des justificatifs reçus.
            if (validation.TransactionId is { } txId && idx < lignes.Count - 3)
            {
                var tx = await _db.Transactions.FirstAsync(t => t.Id == txId);
                tx.ValiderRevision(_financier.Id);
                tx.MettreAJour(_financier.Id, statut: StatutTransaction.Justifiee, statutJustificatif: StatutJustificatif.Present,
                    compte: type == "ticket" ? "625700" : "606400");
                await StampAsync(quand.AddDays(1).AddHours(-7));
            }
            idx++;
        }
        _log.LogInformation("{Count} justificatifs scannés et traités par OCR.", idx);
    }

    // ------------------------------------------------------------------ paie et comptabilité

    private async Task PaieEtComptabiliteAsync()
    {
        var employes = new[]
        {
            EmployePaie.Creer(_eid, "NEO-001", "Leila Ben Salem", "Gérante", "Direction", 5200m, 600m, 300m, "05874123", "1234567-01", "TN59 0800 6000 0110 2233 4455"),
            EmployePaie.Creer(_eid, "NEO-002", "Mouna Khelifi", "Responsable administratif et financier", "Finance", 3600m, 350m, 150m, "06987412", "2345678-02", "TN59 0800 6000 0220 3344 5566"),
            EmployePaie.Creer(_eid, "NEO-003", "Karim Ayari", "Administrateur systèmes", "IT", 3100m, 300m, 120m, "07412589", "3456789-03", "TN59 0800 6000 0330 4455 6677"),
            EmployePaie.Creer(_eid, "NEO-004", "Sami Bouaziz", "Chargé d'affaires", "Commercial", 2400m, 450m, 200m, "08523691", "4567890-04", "TN59 0800 6000 0440 5566 7788"),
            EmployePaie.Creer(_eid, "NEO-005", "Youssef Gharbi", "Commercial terrain", "Commercial", 1900m, 380m, 200m, "09632587", "5678901-05", "TN59 0800 6000 0550 6677 8899"),
            EmployePaie.Creer(_eid, "NEO-006", "Rim Chaabane", "Comptable", "Finance", 2100m, 150m, 80m, "07896541", "6789012-06", "TN59 0800 6000 0660 7788 9900"),
            EmployePaie.Creer(_eid, "NEO-007", "Hatem Mzoughi", "Ingénieur développement", "Projets", 2800m, 250m, 100m, "06325874", "7890123-07", "TN59 0800 6000 0770 8899 0011"),
            EmployePaie.Creer(_eid, "NEO-008", "Amira Sassi", "Ingénieure intégration ERP", "Projets", 2650m, 250m, 100m, "05214789", "8901234-08", "TN59 0800 6000 0880 9900 1122"),
        };
        _db.EmployesPaie.AddRange(employes);
        await StampAsync(new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc));

        var mois = Enumerable.Range(0, 7).Select(i => new DateTime(_debut.Year, _debut.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i)).Where(m => m <= _now).ToList();
        foreach (var m in mois)
        {
            var finMois = m.AddMonths(1).AddDays(-1);
            var enCours = finMois.Date >= _now.Date;
            var periode = m.ToString("yyyy-MM");
            var bulletins = employes.Select(e => BulletinPaie.Generer(e, periode)).ToList();
            _db.BulletinsPaie.AddRange(bulletins);
            await StampAsync(J(enCours ? _now.Date.AddDays(-1) : finMois.AddDays(-3), 15));
            if (enCours) continue;
            foreach (var b in bulletins) b.Valider();
            await StampAsync(J(finMois.AddDays(-2), 11));
            var paiement = Ouvre(finMois.AddDays(-1));
            foreach (var b in bulletins) b.Payer(J(paiement, 9), $"VIR-SAL-{m:yyyyMM}");
            await StampAsync(J(paiement, 9, 30));

            var brut = bulletins.Sum(b => b.Brut);
            var cnss = bulletins.Sum(b => b.CnssSalarie);
            var irpp = bulletins.Sum(b => b.Irpp + b.Css);
            var net = bulletins.Sum(b => b.NetAPayer);
            var patronal = Math.Round(brut * 0.1657m, 3);
            _db.EcrituresComptables.AddRange(EcritureComptable.CreerPiece(_eid, _financier.Id, finMois, "OD", $"SAL-{m:yyyyMM}", $"Salaires {m:MM/yyyy}", "Personnel",
                new[] { ("641000", brut, 0m), ("453000", 0m, cnss), ("434200", 0m, irpp), ("421000", 0m, net) }));
            _db.EcrituresComptables.AddRange(EcritureComptable.CreerPiece(_eid, _financier.Id, finMois, "OD", $"CHS-{m:yyyyMM}", $"Charges patronales CNSS {m:MM/yyyy}", "CNSS",
                new[] { ("645000", patronal, 0m), ("453000", 0m, patronal) }));
            _db.EcrituresComptables.AddRange(EcritureComptable.CreerPiece(_eid, _financier.Id, paiement, "BQ", $"VIR-SAL-{m:yyyyMM}", $"Virement des salaires {m:MM/yyyy}", "Personnel",
                new[] { ("421000", net, 0m), ("532000", 0m, net) }));
            if (m.Month % 3 == 0)
                _db.EcrituresComptables.AddRange(EcritureComptable.CreerPiece(_eid, _financier.Id, finMois, "OD", $"AMO-{m:yyyyMM}", $"Dotation aux amortissements T{(m.Month + 2) / 3}", null,
                    new[] { ("681100", 2843.750m, 0m), ("282800", 0m, 2843.750m) }));
            await StampAsync(J(finMois, 17));
        }

        _db.ActifsComptables.AddRange(
            ActifComptable.Creer(_eid, "Serveur Dell PowerEdge R450", "Matériel informatique", "228300", 33.33m, 18400m, new DateTime(2025, 4, 10, 0, 0, 0, DateTimeKind.Utc), "En service"),
            ActifComptable.Creer(_eid, "Véhicule utilitaire Peugeot Partner", "Matériel de transport", "224000", 20m, 62500m, new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc), "En service"),
            ActifComptable.Creer(_eid, "Mobilier de bureau open space", "Mobilier", "228400", 10m, 14800m, new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc), "En service"),
            ActifComptable.Creer(_eid, "Ordinateurs portables équipe projets (x6)", "Matériel informatique", "228300", 33.33m, 21600m, new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc), "En service"),
            ActifComptable.Creer(_eid, "Licence logiciel de développement", "Immobilisations incorporelles", "213000", 33.33m, 7200m, new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc), "En service"));
        await StampAsync(_debut.AddDays(-5));
    }

    // ------------------------------------------------------------------ calendrier

    private async Task CalendrierAsync()
    {
        DateOnly D(DateTime d) => DateOnly.FromDateTime(d);
        var t = _now.Date;
        void E(DateTime d, string titre, string type, string? debut, string? fin, string? client = null, decimal? montant = null, string? zoom = null, int? rappel = 30)
            => _db.CalendrierEvents.Add(CalendrierEvent.Creer(_eid, titre, D(d), type, debut, fin, client, montant, zoom, rappel));

        foreach (var m in Enumerable.Range(-5, 7))
        {
            var mois = new DateTime(t.Year, t.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(m);
            E(mois.AddDays(27), "Déclaration et paiement de la TVA", "fiscal", "09:00", "10:00", rappel: 1440);
            E(mois.AddDays(14), "Déclaration des retenues à la source", "fiscal", "09:00", "09:30", rappel: 1440);
        }
        E(t.AddDays(1), "Comité de pilotage ERP - Dar El Fanni", "meeting", "10:00", "11:30", "Dar El Fanni Industries SA");
        E(t.AddDays(2), "Démonstration module Comptabilité", "zoom", "15:00", "16:00", "Atlas Agro Export SARL", null, "https://zoom.us/j/84512376921");
        E(t.AddDays(3), "Relance téléphonique Groupe Oliveraie", "overdue", "11:00", "11:15", "Groupe Oliveraie du Centre SA", 18640m);
        E(t.AddDays(5), "Livraison postes de travail Clinique Les Jasmins", "meeting", "09:00", "12:00", "Clinique Les Jasmins");
        E(t.AddDays(6), "Échéance facture Sahel Logistique", "invoice", null, null, "Sahel Logistique SARL", 9840m);
        E(t.AddDays(8), "Formation utilisateurs - Cabinet Ben Romdhane", "meeting", "09:00", "17:00", "Cabinet Ben Romdhane Experts-Comptables");
        E(t.AddDays(10), "Revue trimestrielle avec l'expert-comptable", "meeting", "14:00", "16:00");
        E(t.AddDays(-2), "Kick-off projet Bizerte Marine", "meeting", "10:00", "11:00", "Bizerte Marine Services");
        E(t.AddDays(-6), "Audit sécurité - restitution", "zoom", "16:00", "17:00", "Hôtel Médina Palace", null, "https://zoom.us/j/81234567890");
        E(t.AddDays(-9), "Relance facture Hôtel Médina Palace", "overdue", "10:00", "10:15", "Hôtel Médina Palace", 7420m);

        void T(DateTime d, string titre, string priorite, bool fait)
        {
            var task = CalendrierTask.Creer(_eid, titre, D(d), priorite);
            if (fait) task.Basculer(true);
            _db.CalendrierTasks.Add(task);
        }
        T(t, "Vérifier les justificatifs mobiles en attente", "high", false);
        T(t.AddDays(1), "Préparer l'offre de maintenance 2027 - Sahel Logistique", "normal", false);
        T(t.AddDays(2), "Identifier le virement reçu de 1 250 TND", "high", false);
        T(t.AddDays(3), "Relancer l'assurance pour la facture semestrielle", "normal", false);
        T(t.AddDays(4), "Mettre à jour la grille tarifaire matériel", "low", false);
        T(t.AddDays(7), "Clôture comptable du mois", "high", false);
        T(t.AddDays(-1), "Envoyer les bulletins de paie", "normal", true);
        T(t.AddDays(-3), "Rapprocher le relevé bancaire BIAT", "high", true);
        T(t.AddDays(-5), "Corriger la facture rejetée par TTN", "high", true);
        T(t.AddDays(-8), "Commander les postes HP pour la clinique", "normal", true);
        T(t.AddDays(-12), "Renouveler le certificat de signature", "normal", true);
        await StampAsync(_now.AddDays(-14));
    }

    // ------------------------------------------------------------------ audit et comptes

    private async Task AuditEtComptesAsync()
    {
        var agents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
            "TuniFlowMobile/1.4.2 (Android 14; SM-A546B)"
        };
        var ips = new Dictionary<Guid, string>
        {
            [_responsable.Id] = "197.14.52.18", [_admin.Id] = "197.14.52.21", [_financier.Id] = "197.14.52.30",
            [_ventes.Id] = "41.226.17.88", [_commercial2.Id] = "41.231.8.142"
        };
        // Connexions quotidiennes de l'équipe
        var utilisateurs = new[] { _responsable, _financier, _ventes, _commercial2, _admin };
        for (var d = Ouvre(_debut); d < _now; d = Ouvre(d.AddDays(1)))
            foreach (var u in utilisateurs)
                if (_rng.NextDouble() < (u == _admin ? 0.35 : 0.85))
                    Audit(J(d, 8, _rng.Next(0, 50)), u, "POST", "auth", "/api/auth/login");
        // Actions d'administration
        Audit(_debut.AddDays(3).AddHours(10), _admin, "POST", "utilisateurs", "/api/utilisateurs/invitations");
        Audit(_debut.AddDays(4).AddHours(9), _admin, "PUT", "personnalisation", "/api/personnalisation");
        Audit(_debut.AddDays(30).AddHours(15), _admin, "PUT", "parametres-fiscaux", "/api/parametres-fiscaux");
        Audit(_now.AddDays(-20).AddHours(-3), _admin, "POST", "auth", "/api/auth/2fa/activer");

        foreach (var a in _audit.Where(a => a.Quand < _now))
        {
            var ip = ips.GetValueOrDefault(a.Qui.Id, "197.14.52.40");
            var agent = a.Qui == _commercial2 && a.Ressource == "scan" ? agents[2] : agents[_rng.Next(2)];
            var ev = EvenementAudit.Creer(_eid, a.Qui.Id, a.Methode, a.Ressource, Guid.NewGuid().ToString("N")[..16],
                $"{a.Methode} {a.Chemin}", ip, agent, a.Ressource == "auth" && _rng.NextDouble() < 0.02 ? 401 : a.Methode == "POST" ? 201 : 200);
            _db.EvenementsAudit.Add(ev);
            Set(ev, "CreeLe", DateTime.SpecifyKind(a.Quand, DateTimeKind.Utc));
            Set(ev, "Date", DateTime.SpecifyKind(a.Quand, DateTimeKind.Utc));
        }
        await _db.SaveChangesAsync();

        foreach (var u in utilisateurs)
            u.EnregistrerConnexion();
        _entreprise.DefinirScoring(94, JsonSerializer.Serialize(new
        {
            identite = 25, documents = 24, conformiteTeif = 25, activite = 20,
            commentaire = "Dossier complet : RNE, statuts, attestation fiscale et RIB vérifiés."
        }));
        await StampAsync(_now.AddMinutes(-35));
    }
}
