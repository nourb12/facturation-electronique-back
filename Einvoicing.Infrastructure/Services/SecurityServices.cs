





using Einvoicing.Application.Interfaces;
using OtpNet;
using System.Web;

namespace Einvoicing.Infrastructure.Services;





public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    public string Hacher(string motDePasse) =>
        BCrypt.Net.BCrypt.HashPassword(motDePasse, WorkFactor);
    public bool Verifier(string motDePasse, string hash) =>
        BCrypt.Net.BCrypt.Verify(motDePasse, hash);
}









public sealed class TotpService : ITotpService
{
    public string GenererSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    
    
    public string GenererQrCodeUri(string secret, string email, string issuer = "Einvoicing")
    {
        var label = HttpUtility.UrlEncode($"{issuer}:{email}");
        var issuerEnc = HttpUtility.UrlEncode(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuerEnc}&algorithm=SHA1&digits=6&period=30";
    }

    
    public bool ValiderCode(string secret, string code)
    {
        try
        {
            var keyBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(keyBytes);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch { return false; }
    }
}









public sealed class InMemoryRateLimiter : IRateLimiter
{
    private const int MaxTentatives = 5;
    private static readonly TimeSpan Fenetre = TimeSpan.FromMinutes(15);

    private readonly record struct Entree(int Compteur, DateTime PremierEssai);
    private readonly Dictionary<string, Entree> _store = new();
    private readonly object _lock = new();

    
    public bool EstBloque(string cle)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(cle, out var e)) return false;
            if (e.Compteur < MaxTentatives) return false;
            if (DateTime.UtcNow - e.PremierEssai > Fenetre)
            {
                _store.Remove(cle);
                return false;
            }
            return true;
        }
    }

    
    public void EnregistrerEchec(string cle)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(cle, out var e))
                _store[cle] = e with { Compteur = e.Compteur + 1 };
            else
                _store[cle] = new Entree(1, DateTime.UtcNow);
        }
    }

    
    public void Reinitialiser(string cle)
    {
        lock (_lock) _store.Remove(cle);
    }
}