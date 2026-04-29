





using Einvoicing.Domain.Enums;

namespace Einvoicing.Domain.Entities;





public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UtilisateurId { get; private set; }
    public string Token { get; private set; } = "";
    public string JwtId { get; private set; } = "";
    public bool EstUtilise { get; private set; }
    public bool EstRevoque { get; private set; }   
    public string? AdresseIp { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ExpireLe { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Creer(
        Guid utilisateurId, string token, string jwtId,
        string? ip, string? ua,
        int expiryDays = 7)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateurId,
            Token = token,
            JwtId = jwtId,
            EstUtilise = false,
            EstRevoque = false,
            AdresseIp = ip,
            UserAgent = ua,
            CreeLe = DateTime.UtcNow,
            ExpireLe = DateTime.UtcNow.AddDays(expiryDays)
        };
    }

    
    public bool EstValide() => !EstUtilise && !EstRevoque && DateTime.UtcNow < ExpireLe;

    
    public void Utiliser() => EstUtilise = true;

    
    public void Revoquer() => EstRevoque = true;   
}





public sealed class OtpCode
{
    public Guid Id { get; private set; }
    public Guid UtilisateurId { get; private set; }
    public string Code { get; private set; } = "";
    public OtpType Type { get; private set; }
    public bool EstUtilise { get; private set; }
    public int NbEchecs { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ExpireLe { get; private set; }

    private OtpCode() { }

    public static OtpCode Creer(
        Guid utilisateurId, string code, OtpType type,
        int expiryMinutes = 10)
    {
        return new OtpCode
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateurId,
            Code = code,
            Type = type,
            EstUtilise = false,
            NbEchecs = 0,
            CreeLe = DateTime.UtcNow,
            ExpireLe = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    public bool EstValide() => !EstUtilise && NbEchecs < 5 && DateTime.UtcNow < ExpireLe;

    public void Utiliser() => EstUtilise = true;
    public void EnregistrerEchec() => NbEchecs++;
}





public sealed class SessionActive
{
    public Guid Id { get; private set; }
    public Guid UtilisateurId { get; private set; }
    public string RefreshTokenRef { get; private set; } = "";
    public string Appareil { get; private set; } = "";
    public string TypeAppareil { get; private set; } = "";
    public string UserAgent { get; private set; } = "";
    public string Localisation { get; private set; } = "";
    public string AdresseIp { get; private set; } = "";
    public DateTime CreeLe { get; private set; }
    public DateTime DerniereActivite { get; private set; }

    private SessionActive() { }

    public static SessionActive Creer(
        Guid utilisateurId, string refreshTokenRef,
        string userAgent, string? ip)
    {
        return new SessionActive
        {
            Id = Guid.NewGuid(),
            UtilisateurId = utilisateurId,
            RefreshTokenRef = refreshTokenRef,
            UserAgent = userAgent,
            Appareil = ParseAppareil(userAgent),
            TypeAppareil = ParseTypeAppareil(userAgent),
            Localisation = "Tunis, TN",
            AdresseIp = ip ?? "",
            CreeLe = DateTime.UtcNow,
            DerniereActivite = DateTime.UtcNow
        };
    }

    private static string ParseAppareil(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return "Appareil inconnu";
        var l = ua.ToLowerInvariant();
        if (l.Contains("iphone")) return "iPhone";
        if (l.Contains("android")) return "Android";
        if (l.Contains("windows")) return "Windows PC";
        if (l.Contains("mac")) return "Mac";
        return "Navigateur";
    }

    private static string ParseTypeAppareil(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return "Inconnu";
        var l = ua.ToLowerInvariant();
        if (l.Contains("mobile") || l.Contains("android") || l.Contains("iphone"))
            return "Mobile";
        if (l.Contains("tablet") || l.Contains("ipad"))
            return "Tablette";
        return "Desktop";
    }
}
