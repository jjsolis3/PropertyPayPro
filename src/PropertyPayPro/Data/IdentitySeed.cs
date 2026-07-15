using Microsoft.AspNetCore.Identity;
using PropertyPayPro.Models;

namespace PropertyPayPro.Data;

public static class IdentitySeed
{
    public const string AdminRole = "Admin";
    public const string TenantRole = "Tenant";

    private static readonly string[] AllRoles = { AdminRole, TenantRole };

    public static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = config["ADMIN_EMAIL"];
        if (string.IsNullOrWhiteSpace(adminEmail)) return;

        var user = await userManager.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            var password = config["ADMIN_PASSWORD"];
            if (string.IsNullOrWhiteSpace(password)) return;

            user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded) return;
        }

        if (!await userManager.IsInRoleAsync(user, AdminRole))
        {
            await userManager.AddToRoleAsync(user, AdminRole);
        }
    }
}
