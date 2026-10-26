using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockManagement.Application.Security;
using StockManagement.Infrastructure.Identity;

namespace StockManagement.Infrastructure.Authentication;

public static class AuthSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSeeder");

        foreach (var roleName in Roles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() };
                var createRole = await roleManager.CreateAsync(role);
                if (!createRole.Succeeded)
                    throw new InvalidOperationException($"'{roleName}' rolu yaradılmadı: {string.Join("; ", createRole.Errors.Select(x => x.Description))}");
            }

            var existingClaims = await roleManager.GetClaimsAsync(role);
            var rolePermissions = RolePermissionDefaults.For(roleName);
            var allowedPermissions = rolePermissions.ToHashSet(StringComparer.Ordinal);
            foreach (var obsoleteClaim in existingClaims.Where(x => x.Type == "permission" && !allowedPermissions.Contains(x.Value)))
            {
                var removeResult = await roleManager.RemoveClaimAsync(role, obsoleteClaim);
                if (!removeResult.Succeeded)
                    throw new InvalidOperationException($"'{roleName}' rolundan '{obsoleteClaim.Value}' icazəsi silinmədi: {string.Join("; ", removeResult.Errors.Select(x => x.Description))}");
            }

            foreach (var permission in rolePermissions.Except(existingClaims
                         .Where(x => x.Type == "permission").Select(x => x.Value), StringComparer.Ordinal))
            {
                var addClaim = await roleManager.AddClaimAsync(role, new Claim("permission", permission));
                if (!addClaim.Succeeded)
                    throw new InvalidOperationException($"'{roleName}' roluna '{permission}' icazəsi əlavə edilmədi.");
            }
        }

        var email = configuration["Auth:BootstrapAdmin:Email"];
        var userName = configuration["Auth:BootstrapAdmin:UserName"];
        var password = configuration["Auth:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Bootstrap Admin yaradılmadı. Auth:BootstrapAdmin:Email, UserName və Password konfiqurasiya edilməlidir.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FirstName = configuration["Auth:BootstrapAdmin:FirstName"] ?? "System",
                LastName = configuration["Auth:BootstrapAdmin:LastName"] ?? "Administrator",
                EmailConfirmed = true
            };
            var createAdmin = await userManager.CreateAsync(admin, password);
            if (!createAdmin.Succeeded)
                throw new InvalidOperationException("Bootstrap Admin yaradılmadı: " +
                    string.Join("; ", createAdmin.Errors.Select(x => x.Description)));
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            var addAdminRole = await userManager.AddToRoleAsync(admin, Roles.Admin);
            if (!addAdminRole.Succeeded)
                throw new InvalidOperationException("Bootstrap Admin-ə Admin rolu verilmədi.");
        }
    }
}
