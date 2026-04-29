using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Helpers;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Application.Services;





public sealed class NumeroFactureService(ICompteurFactureRepository compteurRepo) : INumeroFactureService
{
    public async Task<string> GenererNumeroAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var compteur = await compteurRepo.ObtenirAsync(entrepriseId, maintenant.Year, maintenant.Month, ct);

        if (compteur is null)
        {
            compteur = Domain.Entities.CompteurFacture.Creer(entrepriseId, maintenant.Year, maintenant.Month);
            await compteurRepo.AjouterAsync(compteur, ct);
        }

        var numero = compteur.Incrementer();
        compteurRepo.MettreAJour(compteur);
        await compteurRepo.SauvegarderAsync(ct);

        return numero;
    }
}





public sealed class TeifService(
    IFactureRepository factureRepo,
    IClientRepository clientRepo,
    IEntrepriseRepository entrepriseRepo
) : ITeifService
{
    private const string VersionTeifCourante = "2024";

    

    public async Task<XmlTeifDto> GenererXmlAsync(
        Guid factureId, Guid entrepriseId, CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new NotFoundException("Facture introuvable.");
        if (facture.EntrepriseId != entrepriseId) throw new AccesRefuseException();

        if (facture.Statut != Domain.Enums.StatutFacture.Validee
            && facture.Statut != Domain.Enums.StatutFacture.Conforme)
            throw new ValidationMetierException("Seule une facture validée peut être exportée en XML TEIF.");

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct)
            ?? throw new NotFoundException("Client introuvable.");
        var entreprise = await entrepriseRepo.ObtenirParIdAsync(entrepriseId, ct)
            ?? throw new NotFoundException("Entreprise introuvable.");

        var xml = BuildXmlTeif(facture, client, entreprise);
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
            facture.MontantPaye, facture.MontantRestant, facture.EstEnRetard,
            facture.Notes, facture.ConditionsPaiement,
            !string.IsNullOrEmpty(facture.XmlTeif),
            facture.VersionTeif, lignes, historique,
            facture.CreeLe, facture.ModifieLe);
    }

    
    
    

    private static XDocument BuildXmlTeif(
        Domain.Entities.Facture facture,
        Domain.Entities.Client client,
        Domain.Entities.Entreprise entreprise)
    {
        XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ubl + "Invoice",
                new XAttribute(XNamespace.Xmlns + "ubl", ubl),
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),

                
                new XElement(cbc + "UBLVersionID", "2.1"),
                new XElement(cbc + "CustomizationID", "urn:cen.eu:en16931:2017"),
                new XElement(cbc + "ProfileID", "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0"),
                new XElement(cbc + "ID", facture.Numero),
                new XElement(cbc + "IssueDate", facture.DateEmission.ToString("yyyy-MM-dd")),
                new XElement(cbc + "DueDate", facture.DateEcheance.ToString("yyyy-MM-dd")),
                new XElement(cbc + "InvoiceTypeCode", facture.TypeFacture == Domain.Enums.TypeFacture.Avoir ? "381" : "380"),
                new XElement(cbc + "DocumentCurrencyCode", facture.Devise),
                new XElement(cbc + "TaxCurrencyCode", facture.Devise),

                
                new XElement(cac + "AccountingSupplierParty",
                    new XElement(cac + "Party",
                        new XElement(cac + "PartyName",
                            new XElement(cbc + "Name", entreprise.Nom)),
                        new XElement(cac + "PostalAddress",
                            new XElement(cbc + "StreetName", entreprise.Adresse),
                            new XElement(cbc + "CityName", entreprise.Ville),
                            new XElement(cbc + "PostalZone", entreprise.CodePostal),
                            new XElement(cac + "Country",
                                new XElement(cbc + "IdentificationCode", entreprise.Pays))),
                        new XElement(cac + "PartyTaxScheme",
                            new XElement(cbc + "CompanyID", entreprise.MatriculeFiscal),
                            new XElement(cac + "TaxScheme",
                                new XElement(cbc + "ID", "TVA"))),
                        new XElement(cac + "PartyLegalEntity",
                            new XElement(cbc + "RegistrationName", entreprise.Nom),
                            new XElement(cbc + "CompanyID", entreprise.MatriculeFiscal)),
                        new XElement(cac + "Contact",
                            new XElement(cbc + "ElectronicMail", entreprise.Email)))),

                
                new XElement(cac + "AccountingCustomerParty",
                    new XElement(cac + "Party",
                        new XElement(cac + "PartyName",
                            new XElement(cbc + "Name", client.Nom)),
                        new XElement(cac + "PostalAddress",
                            new XElement(cbc + "StreetName", client.Adresse ?? ""),
                            new XElement(cbc + "CityName", client.Ville ?? ""),
                            new XElement(cbc + "PostalZone", client.CodePostal ?? ""),
                            new XElement(cac + "Country",
                                new XElement(cbc + "IdentificationCode", client.Pays))),
                        client.MatriculeFiscal != null
                            ? new XElement(cac + "PartyTaxScheme",
                                new XElement(cbc + "CompanyID", client.MatriculeFiscal),
                                new XElement(cac + "TaxScheme",
                                    new XElement(cbc + "ID", "TVA")))
                            : null!,
                        new XElement(cac + "Contact",
                            new XElement(cbc + "ElectronicMail", client.Email)))),

                
                new XElement(cac + "PaymentMeans",
                    new XElement(cbc + "PaymentMeansCode", MapModePaiement(facture.ModePaiement)),
                    new XElement(cbc + "PaymentDueDate", facture.DateEcheance.ToString("yyyy-MM-dd"))),

                
                BuildTaxTotals(facture, cac, cbc),

                
                new XElement(cac + "LegalMonetaryTotal",
                    new XElement(cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", facture.Devise), facture.TotalHt.ToString("F3")),
                    new XElement(cbc + "TaxExclusiveAmount",
                        new XAttribute("currencyID", facture.Devise), facture.TotalHt.ToString("F3")),
                    new XElement(cbc + "TaxInclusiveAmount",
                        new XAttribute("currencyID", facture.Devise), facture.TotalTtc.ToString("F3")),
                    new XElement(cbc + "PayableAmount",
                        new XAttribute("currencyID", facture.Devise), facture.TotalTtc.ToString("F3"))),

                
                BuildLignes(facture, cac, cbc)
            )
        );

        return doc;
    }

    private static XElement BuildTaxTotals(
        Domain.Entities.Facture facture, XNamespace cac, XNamespace cbc)
    {
        var tvaParTaux = facture.Lignes
            .GroupBy(l => l.TauxTva)
            .Select(g => new
            {
                Taux = g.Key,
                BaseHt = g.Sum(l => l.MontantHt),
                MontantTva = g.Sum(l => l.MontantTva)
            });

        return new XElement(cac + "TaxTotal",
            new XElement(cbc + "TaxAmount",
                new XAttribute("currencyID", facture.Devise),
                facture.TotalTva.ToString("F3")),
            tvaParTaux.Select(t =>
                new XElement(cac + "TaxSubtotal",
                    new XElement(cbc + "TaxableAmount",
                        new XAttribute("currencyID", facture.Devise), t.BaseHt.ToString("F3")),
                    new XElement(cbc + "TaxAmount",
                        new XAttribute("currencyID", facture.Devise), t.MontantTva.ToString("F3")),
                    new XElement(cac + "TaxCategory",
                        new XElement(cbc + "ID", "S"),
                        new XElement(cbc + "Percent", t.Taux.ToString("F1")),
                        new XElement(cac + "TaxScheme",
                            new XElement(cbc + "ID", "TVA"))))));
    }

    private static IEnumerable<XElement> BuildLignes(
        Domain.Entities.Facture facture, XNamespace cac, XNamespace cbc)
    {
        return facture.Lignes.Select(l =>
            new XElement(cac + "InvoiceLine",
                new XElement(cbc + "ID", l.Ordre.ToString()),
                new XElement(cbc + "InvoicedQuantity",
                    new XAttribute("unitCode", l.Unite), l.Quantite.ToString("F3")),
                new XElement(cbc + "LineExtensionAmount",
                    new XAttribute("currencyID", facture.Devise), l.MontantHt.ToString("F3")),
                l.TauxRemise > 0
                    ? new XElement(cac + "AllowanceCharge",
                        new XElement(cbc + "ChargeIndicator", "false"),
                        new XElement(cbc + "Amount",
                            new XAttribute("currencyID", facture.Devise),
                            l.MontantRemise.ToString("F3")))
                    : null!,
                new XElement(cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount",
                        new XAttribute("currencyID", facture.Devise), l.MontantTva.ToString("F3"))),
                new XElement(cac + "Item",
                    new XElement(cbc + "Name", l.Designation),
                    l.Description != null
                        ? new XElement(cbc + "Description", l.Description)
                        : null!,
                    new XElement(cac + "ClassifiedTaxCategory",
                        new XElement(cbc + "ID", "S"),
                        new XElement(cbc + "Percent", l.TauxTva.ToString("F1")),
                        new XElement(cac + "TaxScheme",
                            new XElement(cbc + "ID", "TVA")))),
                new XElement(cac + "Price",
                    new XElement(cbc + "PriceAmount",
                        new XAttribute("currencyID", facture.Devise), l.PrixUnitaire.ToString("F3")))));
    }

    private static string MapModePaiement(Domain.Enums.ModePaiement mode) => mode switch
    {
        Domain.Enums.ModePaiement.Virement => "30",
        Domain.Enums.ModePaiement.Cheque => "20",
        Domain.Enums.ModePaiement.Especes => "10",
        Domain.Enums.ModePaiement.CarteBancaire => "48",
        Domain.Enums.ModePaiement.Traite => "57",
        _ => "1"
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

        
        var totalLignesHt = Math.Round(facture.Lignes.Sum(l => l.MontantHt), 3);
        if (Math.Abs(totalLignesHt - facture.TotalHt) > 0.01m)
            erreurs.Add(new("R009", $"Le total HT ({facture.TotalHt}) ne correspond pas à la somme des lignes ({totalLignesHt}).", "TotalHt", "Erreur"));

        
        var devisesValides = new[] { "TND", "EUR", "USD" };
        if (!devisesValides.Contains(facture.Devise))
            erreurs.Add(new("R010", $"Devise '{facture.Devise}' non reconnue.", "Devise", "Avertissement"));

        
        var tauxNonConformes = facture.Lignes
            .Where(l => l.TauxTva != 0 && l.TauxTva != 7 && l.TauxTva != 13 && l.TauxTva != 19)
            .ToList();
        foreach (var l in tauxNonConformes)
            erreurs.Add(new("R011", $"Taux TVA {l.TauxTva}% non conforme aux taux tunisiens (0, 7, 13, 19%).", "TauxTva", "Avertissement"));

        
        if (string.IsNullOrWhiteSpace(entreprise.Email))
            erreurs.Add(new("R012", "L'email de l'entreprise est obligatoire.", "Email", "Erreur"));
    }

    private static string ComputerHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
