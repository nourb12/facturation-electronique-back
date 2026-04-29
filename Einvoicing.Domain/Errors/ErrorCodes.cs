namespace Einvoicing.Domain.Errors;

/// <summary>
/// Stable, i18n-friendly error keys returned by the API.
/// The frontend translates these keys based on the current language.
/// </summary>
public static class ErrorCodes
{
    // Generic
    public const string Generic = "ERRORS.GENERIC";
    public const string InvalidData = "ERRORS.INVALID_DATA";
    public const string AccessDenied = "ERRORS.ACCESS_DENIED";

    // Auth
    public const string InvalidCredentials = "ERRORS.INVALID_CREDENTIALS";
    public const string AccountInactive = "ERRORS.ACCOUNT_INACTIVE";
    public const string TokenInvalid = "ERRORS.TOKEN_INVALID";
    public const string OtpInvalidOrExpired = "ERRORS.OTP_INVALID_OR_EXPIRED";
    public const string InvalidOtp = "ERRORS.INVALID_OTP";
    public const string TooManyAttempts = "ERRORS.TOO_MANY_ATTEMPTS";

    // Uniqueness / conflicts
    public const string ConflictData = "ERRORS.CONFLICT_DATA";
    public const string MatriculeAlreadyUsed = "ERRORS.MATRICULE_ALREADY_USED";
    public const string EmailAlreadyUsed = "ERRORS.EMAIL_ALREADY_USED";
    public const string UniqueConstraintViolation = "ERRORS.UNIQUE_CONSTRAINT_VIOLATION";

    // Validation
    public const string FirstNameRequired = "ERRORS.FIRST_NAME_REQUIRED";
    public const string LastNameRequired = "ERRORS.LAST_NAME_REQUIRED";
    public const string EmailRequired = "ERRORS.EMAIL_REQUIRED";
    public const string EmailInvalid = "ERRORS.EMAIL_INVALID";
    public const string PasswordRequired = "ERRORS.PASSWORD_REQUIRED";
    public const string PasswordMin6 = "ERRORS.PASSWORD_MIN_6";
    public const string PasswordMin8 = "ERRORS.PASSWORD_MIN_8";
    public const string PasswordNeedsUpper = "ERRORS.PASSWORD_NEEDS_UPPER";
    public const string PasswordNeedsDigit = "ERRORS.PASSWORD_NEEDS_DIGIT";
    public const string PasswordNeedsSpecial = "ERRORS.PASSWORD_NEEDS_SPECIAL";
    public const string PasswordsMismatch = "ERRORS.PASSWORDS_MISMATCH";
    public const string CompanyNameRequired = "ERRORS.COMPANY_NAME_REQUIRED";
    public const string MatriculeRequired = "ERRORS.MATRICULE_REQUIRED";
    public const string MatriculeInvalid = "ERRORS.MATRICULE_INVALID";
    public const string AddressRequired = "ERRORS.ADDRESS_REQUIRED";
    public const string GovernorateRequired = "ERRORS.GOV_REQUIRED";
    public const string PostalRequired = "ERRORS.POSTAL_REQUIRED";
    public const string PostalInvalid = "ERRORS.POSTAL_INVALID";
    public const string PhoneRequired = "ERRORS.PHONE_REQUIRED";

    // Business validation
    public const string CurrencyRequired = "ERRORS.CURRENCY_REQUIRED";
    public const string CurrencyInvalid = "ERRORS.CURRENCY_INVALID";
    public const string LegalFormRequired = "ERRORS.LEGAL_FORM_REQUIRED";
    public const string FunctionRequired = "ERRORS.FUNCTION_REQUIRED";

    // Documents (KYC / onboarding)
    public const string RegistreCommerceRequired = "ERRORS.DOC_REGISTRE_COMMERCE_REQUIRED";
    public const string PatenteRequired = "ERRORS.DOC_PATENTE_REQUIRED";
    public const string CinResponsableRequired = "ERRORS.DOC_CIN_REQUIRED";
    public const string RibRequired = "ERRORS.DOC_RIB_REQUIRED";
}
