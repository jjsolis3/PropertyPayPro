using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PropertyPayPro.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(80)]
    public string? FirstName { get; set; }

    [StringLength(80)]
    public string? LastName { get; set; }

    [StringLength(500)]
    public string? AvatarStorageKey { get; set; }

    // Link to the Tenant record this login belongs to. Null for admin/manager
    // users; populated for tenant-portal logins so their pages can be scoped.
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string DisplayName
    {
        get
        {
            var combined = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? (UserName ?? Email ?? "User") : combined;
        }
    }
}
