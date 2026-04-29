





using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Einvoicing.Infrastructure.Services;

public sealed class JwtService(
    IConfiguration configuration,
    ILogger<JwtService> logger) : IJwtService
{
    private string Secret => configuration["Jwt:Secret"]!;
    private string Issuer => configuration["Jwt:Issuer"]!;
    private string Audience => configuration["Jwt:Audience"]!;
    private int Expiry =>
        int.TryParse(configuration["Jwt:ExpiryMinutes"], out var v1) ? v1 :
        int.TryParse(configuration["Jwt:ExpirationMinutes"], out var v2) ? v2 : 60;

    private SymmetricSecurityKey Cle => new(Encoding.UTF8.GetBytes(Secret));

    
    
    
    public string GenererAccessToken(Utilisateur utilisateur)
    {
        var jwtId = Guid.NewGuid().ToString();

        var claims = new[]
        {
            
            new Claim("userId",                      utilisateur.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, utilisateur.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   jwtId),
            new Claim(ClaimTypes.Role,               utilisateur.Role.ToString()),
            new Claim("prenom",                      utilisateur.Prenom),
            new Claim("nom",                         utilisateur.Nom),
            new Claim("entrepriseId",                utilisateur.EntrepriseId?.ToString() ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(Expiry),
            signingCredentials: new SigningCredentials(Cle, SecurityAlgorithms.HmacSha256)
        );

        var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);
        logger.LogInformation("✅ Token généré — JwtId={JwtId} User={Email}", jwtId, utilisateur.Email);
        return tokenStr;
    }

    
    
    
    public string GenererRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    
    
    
    public ClaimsResult? ExtraireClaimsTokenExpire(string accessToken)
    {
        try
        {
            
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Cle,
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            
            var userId = principal.FindFirst("userId")?.Value;
            var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "";

            if (userId is null || jwtId is null)
            {
                logger.LogWarning("❌ Claims manquants — userId={UserId} jti={Jti}", userId, jwtId);
                return null;
            }

            logger.LogInformation("✅ Claims extraits — UserId={UserId} JwtId={JwtId}", userId, jwtId);
            return new ClaimsResult(Guid.Parse(userId), jwtId, role);
        }
        catch (Exception ex)
        {
            logger.LogError("❌ Extraction échouée : {Message}", ex.Message);
            return null;
        }
    }
}