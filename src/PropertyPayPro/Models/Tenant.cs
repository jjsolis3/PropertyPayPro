using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public class Tenant
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress, StringLength(160)]
    public string? Email { get; set; }

    [Phone, StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<Lease> Leases { get; set; } = new();

    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
