




using Einvoicing.Application.Interfaces;
using System.Security.Claims;

namespace Einvoicing.Api.Middleware;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    
    public Guid? UtilisateurId
    {
        get
        {
            var val = User?.FindFirstValue("userId");
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public Guid? EntrepriseId
    {
        get
        {
            var val = User?.FindFirstValue("entrepriseId");
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email)
                         ?? User?.FindFirstValue("email");

    public string? Role => User?.FindFirstValue(ClaimTypes.Role)
                         ?? User?.FindFirstValue("role");

    public bool EstAuthentifie => User?.Identity?.IsAuthenticated ?? false;
}