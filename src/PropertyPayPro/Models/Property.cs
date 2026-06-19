using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public class Property
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string State { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<Lease> Leases { get; set; } = new();
}
