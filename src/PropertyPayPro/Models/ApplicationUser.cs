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

    public string DisplayName
    {
        get
        {
            var combined = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? (UserName ?? Email ?? "User") : combined;
        }
    }
}
