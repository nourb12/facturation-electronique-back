




using Einvoicing.Domain.Errors;

namespace Einvoicing.Domain.Exceptions;

public class IdentifiantsInvalidesException : Exception
{
    public IdentifiantsInvalidesException()
        : base(ErrorCodes.InvalidCredentials) { }
}

public class CompteInactifException : Exception
{
    public CompteInactifException()
        : base(ErrorCodes.AccountInactive) { }
}

public class OtpInvalideException : Exception
{
    public OtpInvalideException()
        : base(ErrorCodes.OtpInvalidOrExpired) { }
}

public class TokenInvalideException : Exception
{
    public TokenInvalideException()
        : base(ErrorCodes.TokenInvalid) { }
}

public class TropDeTentativesException : Exception
{
    public int SecondesRestantes { get; }

    public TropDeTentativesException(int secondesRestantes = 900)
        : base(ErrorCodes.TooManyAttempts)
    {
        SecondesRestantes = secondesRestantes;
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message) { }
}
public class AccesRefuseException : Exception
{
    public AccesRefuseException(string message = ErrorCodes.AccessDenied)
        : base(message) { }
}

public class ConflitException : Exception
{
    public ConflitException(string message)
        : base(message) { }
}

public class ValidationMetierException : Exception
{
    public ValidationMetierException(string message)
        : base(message) { }
}
