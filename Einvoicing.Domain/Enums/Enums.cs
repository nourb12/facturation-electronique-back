namespace Einvoicing.Domain.Enums;

public enum RoleUtilisateur
{
    SuperAdmin = 0,
    Admin = 1,
    ResponsableEntreprise = 2,
    ResponsableFinancier = 3
}

public enum StatutCompte
{
    Actif = 0,
    Inactif = 1,
    Suspendu = 2,
    EnAttente = 3,
    Supprime = 4
}

public enum OtpType
{
    ReinitialisationMotDePasse = 0,
    VerificationEmail = 1,
    Connexion2FA = 2
}

public enum RegimeFiscal
{
    Reel = 0,
    Forfait = 1,
    NonAssujetti = 2
}

public enum TypeClient
{
    B2B = 0,
    B2C = 1,
    B2G = 2
}

public enum TypeProduit
{
    Produit = 0,
    Service = 1
}

public enum StatutFacture
{
    Brouillon = 0,
    Validee = 1,
    Conforme = 2,
    Transmise = 3,
    Acceptee = 4,
    Rejetee = 5,
    Payee = 6,
    PartiellemementPayee = 7,
    Annulee = 8
}

public enum TypeFacture
{
    Facture = 0,
    Avoir = 1,
    Proforma = 2
}

public enum ModePaiement
{
    Virement = 0,
    Cheque = 1,
    Especes = 2,
    CarteBancaire = 3,
    Traite = 4
}

public enum StatutSignature
{
    EnAttente = 0,
    Signee = 1,
    Echec = 2,
    EchecDefinitif = 3
}

public enum StatutEchange
{
    EnAttente = 0,
    Envoye = 1,
    Acknowledge = 2,
    Accepte = 3,
    Rejete = 4,
    Erreur = 5,
    EchecDefinitif = 6
}

public enum TypeParametreFiscal
{
    Pourcentage = 0,
    Fixe = 1
}

public enum SigneParametreFiscal
{
    Positif = 0,
    Negatif = 1
}

public enum OrdreCalcul
{
    AvantTva = 0,
    ApresTva = 1
}

public enum UtilisationParametreFiscal
{
    Manuel = 0,
    Auto = 1
}

public enum TypeTaxe
{
    Tva = 0,
    Fodec = 1,
    DroitConsommation = 2
}

public enum TypeTransaction
{
    Entree = 0,
    Sortie = 1
}

public enum StatutTransaction
{
    NonJustifiee = 0,
    EnAttente = 1,
    Justifiee = 2
}

public enum StatutJustificatif
{
    Facultatif = 0,
    Perdu = 1,
    Present = 2
}

public enum DocumentSource
{
    Web = 0,
    MobileApp = 1
}

public enum ScannedDocumentStatus
{
    Uploaded = 0,
    MobileReviewed = 1,
    LinkedToTransaction = 2
}

public enum DemoRequestStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3
}
