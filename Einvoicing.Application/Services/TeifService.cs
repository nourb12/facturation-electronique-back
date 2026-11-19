using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Exceptions;

using Einvoicing.Domain;

namespace Einvoicing.Application.Services;





public sealed class NumeroFactureService(ICompteurFactureRepository compteurRepo) : INumeroFactureService
{
    public async Task<string> GenererNumeroAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var compteur = await compteurRepo.IncrementerEtObtenirAsync(
            entrepriseId,
            maintenant.Year,
            maintenant.Month,
            ct);

        return $"{compteur.Prefixe}-{maintenant.Year}{maintenant.Month:D2}-{compteur.DernierNumero:D4}";
    }
}





public sealed class TeifService(
    IFactureRepository factureRepo,
    IClientRepository clientRepo,
    IEntrepriseRepository entrepriseRepo
) : ITeifService
{
    private const string VersionTeifCourante = VersionSchemaTeif;

    

    public async Task<XmlTeifDto> GenererXmlAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        if (facture.TypeFacture is Domain.Enums.TypeFacture.Devis or Domain.Enums.TypeFacture.Proforma)
            throw new ValidationMetierException("Convertissez le devis ou la proforma en facture avant l'export fiscal XML.");

        if (facture.Statut != Domain.Enums.StatutFacture.Validee
            && facture.Statut != Domain.Enums.StatutFacture.Conforme)
            throw new ValidationMetierException("Seule une facture validée peut être exportée en XML TEIF.");

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)
            ?? throw new NotFoundException("Client introuvable.");
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        string? numeroOrigine = null;
        if (facture.FactureOrigineId.HasValue)
            numeroOrigine = (await factureRepo.ObtenirParIdAsync(facture.FactureOrigineId.Value, ct))?.Numero;

        var xml = BuildXmlTeif(facture, client, entreprise, numeroOrigine);
        var xmlString = xml.ToString(SaveOptions.None);
        var hash = ComputerHash(xmlString);

        facture.EnregistrerXml(xmlString, hash);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        return new XmlTeifDto(
            facture.Id, facture.Numero,
            xmlString, hash, VersionTeifCourante,
            DateTime.UtcNow);
    }

    

    public async Task<ValidationTeifDto> ValiderConformiteAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)!;
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)!;

        var erreurs = new List<ErreurTeifDto>();

        
        AppliquerRegles(facture, client!, entreprise!, erreurs);

        return new ValidationTeifDto(
            facture.Id,
            !erreurs.Any(e => e.Severite == "Erreur"),
            erreurs,
            DateTime.UtcNow);
    }

    

    public async Task<FactureDto> MarquerConformeAsync(
        Guid factureId, Guid entrepriseId, Guid validePar, CancellationToken ct = default)
    {
        var validation = await ValiderConformiteAsync(factureId, entrepriseId, ct);

        if (!validation.EstConforme)
            throw new ValidationMetierException(
                $"La facture n'est pas conforme TEIF. {validation.Erreurs.Count} erreur(s) trouvée(s).");

        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)!;
        facture!.MarquerConforme(validePar, VersionTeifCourante);
        factureRepo.MettreAJour(facture);
        await factureRepo.SauvegarderAsync(ct);

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)!;
        var lignes = facture.Lignes.Select(l => new LigneFactureDto(
            l.Id, l.Ordre, l.Designation, l.Description, l.Unite,
            l.Quantite, l.PrixUnitaire, l.TauxRemise, l.TauxTva,
            l.MontantHt, l.MontantRemise, l.MontantTva, l.MontantTtc, l.ProduitId)).ToList();

        var historique = facture.Historique.OrderByDescending(h => h.CreeLe)
            .Select(h => new HistoriqueFactureDto(h.Id, h.Action, h.Details,
                h.AncienneValeur, h.NouvelleValeur, h.CreeLe)).ToList();

        return new FactureDto(
            facture.Id, facture.EntrepriseId, facture.ClientId,
            facture.FactureOrigineId,
            client!.Nom, client.MatriculeFiscal,
            facture.Numero, facture.Reference,
            facture.Statut.ToString(), facture.TypeFacture.ToString(),
            facture.ModePaiement.ToString(), facture.Devise,
            facture.DateEmission, facture.DateEcheance, facture.DatePaiement,
            facture.TotalHt, facture.TotalTva, facture.TotalTtc,
            facture.AppliquerRS, facture.CodeRS, facture.TauxRS, facture.BaseRS, facture.MontantRS, facture.NetAPayer,
            facture.MontantPaye, facture.MontantRestant, facture.EstEnRetard,
            facture.Notes, facture.ConditionsPaiement,
            !string.IsNullOrEmpty(facture.XmlTeif),
            facture.VersionTeif, lignes, historique,
            facture.CreeLe, facture.ModifieLe,
            facture.RemiseGlobale, facture.TimbreFiscal);
    }

    
    
    

    // ------------------------------------------------------------------
    // Génération TEIF 1.8.8 (Tunisie TradeNet – El Fatoora), XML sans signature.
    // Structure et codes conformes au schéma officiel teif-1.8.8-withoutSig.xsd
    // et au référentiel de l'annexe A du guide de mise en œuvre TEIF.
    // ------------------------------------------------------------------

    internal const string VersionSchemaTeif = "1.8.8";

    internal static XDocument BuildXmlTeif(
        Domain.Entities.Facture facture,
        Domain.Entities.Client client,
        Domain.Entities.Entreprise entreprise,
        string? numeroFactureOrigine = null)
    {
        var mfVendeur = MatriculeFiscalHelper.Normalize(entreprise.MatriculeFiscal);
        var (typeIdClient, idClient) = IdentifiantClient(client)
            ?? throw new ValidationMetierException(
                "Le client doit avoir un matricule fiscal ou un numéro de CIN (8 chiffres) pour la facture électronique.");
        var estAvoir = facture.TypeFacture == Domain.Enums.TypeFacture.Avoir;

        var body = new XElement("InvoiceBody",
            new XElement("Bgm",
                new XElement("DocumentIdentifier", Tronquer(facture.Numero, 70)),
                new XElement("DocumentType",
                    new XAttribute("code", estAvoir ? "I-12" : "I-11"),
                    estAvoir ? "Facture d'avoir" : "Facture"),
                estAvoir && !string.IsNullOrWhiteSpace(numeroFactureOrigine)
                    ? new XElement("DocumentReferences",
                        new XElement("DocumentReference",
                            new XElement("Reference", new XAttribute("refID", "I-89"), Tronquer(numeroFactureOrigine, 200))))
                    : null),
            new XElement("Dtm",
                DateTeif("I-31", facture.DateEmission),
                DateTeif("I-32", facture.DateEcheance)),
            new XElement("PartnerSection",
                Partenaire("I-62", "I-01", mfVendeur, entreprise.Nom, "Qualification",
                    entreprise.Adresse, entreprise.Ville, entreprise.CodePostal, entreprise.Pays),
                Partenaire("I-64", typeIdClient, idClient, client.Nom,
                    client.TypeClient == Domain.Enums.TypeClient.B2B ? "Qualification" : "Physical",
                    client.Adresse, client.Ville, client.CodePostal, client.Pays)),
            new XElement("PytSection",
                new XElement("PytSectionDetails",
                    new XElement("PytPai",
                        new XElement("PaiConditionCode", "I-121"),
                        new XElement("PaiMeansCode", MapModePaiement(facture.ModePaiement))))),
            TexteLibre(facture),
            new XElement("LinSection", facture.Lignes.OrderBy(l => l.Ordre).Select(l => Ligne(l, facture.Devise))),
            new XElement("InvoiceMoa",
                Montant("I-176", facture.TotalHt, facture.Devise),
                Montant("I-182", facture.TotalHt, facture.Devise),
                Montant("I-181", facture.TotalTva, facture.Devise),
                Montant("I-180", facture.TotalTtc + facture.TimbreFiscal, facture.Devise)),
            new XElement("InvoiceTax",
                facture.VentilerTva().Select(v => new XElement("InvoiceTaxDetails",
                    Taxe("I-1602", "TVA", v.Taux),
                    Montant("I-177", v.BaseHt, facture.Devise),
                    Montant("I-178", v.MontantTva, facture.Devise))),
                facture.TimbreFiscal > 0
                    ? new XElement("InvoiceTaxDetails",
                        Taxe("I-1601", "Droit de timbre", 0),
                        Montant("I-178", facture.TimbreFiscal, facture.Devise))
                    : null));

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("TEIF",
                new XAttribute("version", VersionSchemaTeif),
                new XAttribute("controlingAgency", "TTN"),
                new XElement("InvoiceHeader",
                    new XElement("MessageSenderIdentifier", new XAttribute("type", "I-01"), mfVendeur),
                    new XElement("MessageRecieverIdentifier", new XAttribute("type", typeIdClient), idClient)),
                body));
    }

    /// <summary>
    /// Identifiant du client : matricule fiscal tunisien (I-01), matricule non tunisien (I-04)
    /// ou CIN à 8 chiffres saisie dans le champ matricule (I-02).
    /// </summary>
    internal static (string Type, string Valeur)? IdentifiantClient(Domain.Entities.Client client)
    {
        var brut = (client.MatriculeFiscal ?? string.Empty).Trim();
        if (brut.Length == 0) return null;
        if (System.Text.RegularExpressions.Regex.IsMatch(brut, @"^[0-9]{8}$", System.Text.RegularExpressions.RegexOptions.None, RegexMatchPolicy.Timeout)) return ("I-02", brut);
        if (!string.Equals(client.Pays, "TN", StringComparison.OrdinalIgnoreCase)) return ("I-04", Tronquer(brut, 35));
        return ("I-01", MatriculeFiscalHelper.Normalize(brut));
    }

    private static XElement DateTeif(string fonction, DateTime date)
        => new("DateText",
            new XAttribute("functionCode", fonction),
            new XAttribute("format", "ddMMyy"),
            date.ToString("ddMMyy", CultureInfo.InvariantCulture));

    private static XElement Partenaire(string fonction, string typeId, string id, string nom, string typeNom,
        string? adresse, string? ville, string? codePostal, string? pays)
    {
        var description = string.Join(" ", new[] { adresse, codePostal, ville }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return new XElement("PartnerDetails",
            new XAttribute("functionCode", fonction),
            new XElement("Nad",
                new XElement("PartnerIdentifier", new XAttribute("type", typeId), Tronquer(id, 35)),
                new XElement("PartnerName", new XAttribute("nameType", typeNom), Tronquer(nom, 200)),
                new XElement("PartnerAdresses",
                    new XAttribute("lang", "fr"),
                    new XElement("AdressDescription", Tronquer(description, 500)),
                    string.IsNullOrWhiteSpace(adresse) ? null : new XElement("Street", Tronquer(adresse, 35)),
                    string.IsNullOrWhiteSpace(ville) ? null : new XElement("CityName", Tronquer(ville, 35)),
                    string.IsNullOrWhiteSpace(codePostal) ? null : new XElement("PostalCode", Tronquer(codePostal, 17)),
                    new XElement("Country", new XAttribute("codeList", "ISO_3166-1"),
                        string.IsNullOrWhiteSpace(pays) ? "TN" : Tronquer(pays.Trim().ToUpperInvariant(), 6)))));
    }

    /// <summary>Notes et remise globale en texte libre (observation I-48).</summary>
    private static XElement? TexteLibre(Domain.Entities.Facture facture)
    {
        var textes = new List<string>();
        if (facture.RemiseGlobale > 0)
            textes.Add($"Remise globale de {Nombre(facture.RemiseGlobale)} {facture.Devise} répartie sur les bases de TVA.");
        if (!string.IsNullOrWhiteSpace(facture.Notes)) textes.Add(Tronquer(facture.Notes, 500));
        if (!string.IsNullOrWhiteSpace(facture.ConditionsPaiement)) textes.Add(Tronquer(facture.ConditionsPaiement, 500));
        if (textes.Count == 0) return null;
        return new XElement("Ftx",
            new XElement("FreeTextDetail",
                new XAttribute("subjectCode", "I-48"),
                textes.Select(t => new XElement("FreeTexts", t))));
    }

    private static XElement Ligne(Domain.Entities.LigneFacture l, string devise)
        => new("Lin",
            new XElement("ItemIdentifier", l.Ordre.ToString(CultureInfo.InvariantCulture)),
            new XElement("LinImd",
                new XAttribute("lang", "fr"),
                new XElement("ItemCode", l.ProduitId?.ToString("N")[..12].ToUpperInvariant() ?? $"L{l.Ordre:D3}"),
                new XElement("ItemDescription", Tronquer(l.Designation, 500))),
            new XElement("LinQty",
                new XElement("Quantity",
                    new XAttribute("measurementUnit", UniteTeif(l.Unite)),
                    Nombre(l.Quantite))),
            Taxe("I-1602", "TVA", l.TauxTva, "LinTax"),
            l.TauxRemise > 0
                ? new XElement("LinAlc",
                    new XElement("Alc",
                        new XAttribute("allowanceCode", "I-151"),
                        new XElement("SpecialServices", new XAttribute("lang", "fr"), "Remise")),
                    new XElement("Pcd",
                        new XElement("Percentage", Taux(l.TauxRemise)),
                        new XElement("PercentageBasis", Nombre(l.MontantHt + l.MontantRemise))))
                : null,
            new XElement("LinMoa",
                MontantDetail("MoaDetails", "I-183", l.PrixUnitaire, devise),
                MontantDetail("MoaDetails", "I-171", l.MontantHt, devise)));

    private static XElement Taxe(string code, string libelle, decimal taux, string nom = "Tax")
        => new(nom,
            new XElement("TaxTypeName", new XAttribute("code", code), libelle),
            new XElement("TaxDetails", new XElement("TaxRate", Taux(taux))));

    private static XElement Montant(string code, decimal montant, string devise)
        => MontantDetail("AmountDetails", code, montant, devise);

    private static XElement MontantDetail(string conteneur, string code, decimal montant, string devise)
        => new(conteneur,
            new XElement("Moa",
                new XAttribute("currencyCodeList", "ISO_4217"),
                new XAttribute("amountTypeCode", code),
                new XElement("Amount", new XAttribute("currencyIdentifier", devise), Nombre(montant))));

    /// <summary>Montant au format TEIF : point décimal, 3 décimales (millimes).</summary>
    internal static string Nombre(decimal valeur)
        => Math.Round(valeur, 3, MidpointRounding.AwayFromZero).ToString("0.000", CultureInfo.InvariantCulture);

    private static string Taux(decimal taux)
        => Math.Round(taux, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    private static string UniteTeif(string? unite)
    {
        var u = (unite ?? string.Empty).Trim();
        return u.Length == 0 || u.Equals("U", StringComparison.OrdinalIgnoreCase) ? "UNIT" : Tronquer(u, 8);
    }

    private static string Tronquer(string? valeur, int max)
    {
        var v = (valeur ?? string.Empty).Trim();
        return v.Length <= max ? v : v[..max];
    }

    /// <summary>Moyens de paiement, référentiel I-13.</summary>
    private static string MapModePaiement(Domain.Enums.ModePaiement mode) => mode switch
    {
        Domain.Enums.ModePaiement.Especes => "I-131",
        Domain.Enums.ModePaiement.Cheque => "I-132",
        Domain.Enums.ModePaiement.Virement => "I-135",
        _ => "I-137"
    };

    private static void AppliquerRegles(
        Domain.Entities.Facture facture,
        Domain.Entities.Client client,
        Domain.Entities.Entreprise entreprise,
        List<ErreurTeifDto> erreurs)
    {
        
        if (string.IsNullOrWhiteSpace(facture.Numero))
            erreurs.Add(new("R001", "Le numéro de facture est obligatoire.", "Numero", "Erreur"));

        
        if (facture.DateEmission == default)
            erreurs.Add(new("R002", "La date d'émission est obligatoire.", "DateEmission", "Erreur"));

        
        if (facture.DateEcheance.Date < facture.DateEmission.Date)
            erreurs.Add(new("R003", "La date d'échéance doit être postérieure à la date d'émission.", "DateEcheance", "Erreur"));

        
        if (string.IsNullOrWhiteSpace(entreprise.MatriculeFiscal))
            erreurs.Add(new("R004", "Le matricule fiscal de l'entreprise est obligatoire.", "MatriculeFiscal", "Erreur"));

        
        if (!string.IsNullOrWhiteSpace(entreprise.MatriculeFiscal) && !MatriculeFiscalHelper.IsValid(entreprise.MatriculeFiscal))
            erreurs.Add(new("R005", "Le matricule fiscal doit respecter le format 1495908/S ou 1234567A/B/M/000.", "MatriculeFiscal", "Erreur"));

        
        if (client.TypeClient == Domain.Enums.TypeClient.B2B && string.IsNullOrWhiteSpace(client.MatriculeFiscal))
            erreurs.Add(new("R006", "Le matricule fiscal est obligatoire pour un client B2B.", "Client.MatriculeFiscal", "Erreur"));

        
        if (!facture.Lignes.Any())
            erreurs.Add(new("R007", "La facture doit contenir au moins une ligne.", "Lignes", "Erreur"));

        
        if (facture.TotalTtc <= 0)
            erreurs.Add(new("R008", "Le total TTC doit être supérieur à zéro.", "TotalTtc", "Erreur"));

        
        var totalLignesHt = Math.Round(facture.Lignes.Sum(l => l.MontantHt) - facture.RemiseGlobale, 3);
        if (Math.Abs(totalLignesHt - facture.TotalHt) > 0.01m)
            erreurs.Add(new("R009", $"Le total HT ({facture.TotalHt}) ne correspond pas à la somme des lignes moins la remise globale ({totalLignesHt}).", "TotalHt", "Erreur"));

        
        var devisesValides = new[] { "TND", "EUR", "USD" };
        if (!devisesValides.Contains(facture.Devise))
            erreurs.Add(new("R010", $"Devise '{facture.Devise}' non reconnue.", "Devise", "Avertissement"));

        
        var tauxNonConformes = facture.Lignes
            .Where(l => l.TauxTva != 0 && l.TauxTva != 7 && l.TauxTva != 13 && l.TauxTva != 19)
            .ToList();
        foreach (var l in tauxNonConformes)
            erreurs.Add(new("R011", $"Taux TVA {l.TauxTva}% non conforme aux taux tunisiens (0, 7, 13, 19%).", "TauxTva", "Avertissement"));

        
        if (IdentifiantClient(client) is null)
            erreurs.Add(new("R013", "TEIF : le client doit avoir un matricule fiscal ou un numéro de CIN (8 chiffres).", "Client.MatriculeFiscal", "Erreur"));

        if (!string.IsNullOrWhiteSpace(entreprise.MatriculeFiscal) && MatriculeFiscalHelper.Normalize(entreprise.MatriculeFiscal).Length != 13)
            erreurs.Add(new("R014", "TEIF : le matricule fiscal de l'entreprise doit être complet sur 13 caractères (ex. 1234567AAM000).", "MatriculeFiscal", "Avertissement"));

        if (string.IsNullOrWhiteSpace(entreprise.Email))
            erreurs.Add(new("R012", "L'email de l'entreprise est obligatoire.", "Email", "Erreur"));
    }

    private static string ComputerHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
