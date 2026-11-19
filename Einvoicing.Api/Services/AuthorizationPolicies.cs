using Microsoft.AspNetCore.Authorization;

namespace Einvoicing.Api.Services;

public static class AuthorizationPolicies
{
    public static IServiceCollection AddTuniFlowAuthorization(this IServiceCollection services)
    {
        return services.AddAuthorization(options =>
        {
            var authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.DefaultPolicy = authenticated;
            options.FallbackPolicy = authenticated;
            Add("SuperAdmin", "SuperAdmin");
            Add("Admin", "SuperAdmin", "Admin");
            Add("SuperOuAdmin", "SuperAdmin", "Admin");
            Add("ResponsableOuAdmin", "SuperAdmin", "Admin", "ResponsableEntreprise");
            Add("Tous", "SuperAdmin", "Admin", "ResponsableEntreprise", "ResponsableFinancier");
            Add("Ventes", "SuperAdmin", "Admin", "ResponsableEntreprise", "ResponsableFinancier", "CollaborateurVentes");
            Add("RagUser", "SuperAdmin", "Admin", "ResponsableEntreprise", "ResponsableFinancier", "CollaborateurVentes");

            void Add(string name, params string[] roles) => options.AddPolicy(name,
                policy => policy.RequireAuthenticatedUser().RequireRole(roles));
        });
    }
}
