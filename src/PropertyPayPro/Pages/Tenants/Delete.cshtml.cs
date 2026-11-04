using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Tenant Tenant { get; set; } = new();

    /// <summary>Why this tenant can't be deleted, or null when it can.</summary>
    public string? BlockedReason { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Tenants.FindAsync(id);
        if (entity is null) return NotFound();
        Tenant = entity;
        BlockedReason = await BlockedReasonAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Tenants.FindAsync(Tenant.Id);
        if (entity is null) return RedirectToPage("Index");
        // Deleting silently removed the tenant from their leases (and the lease's
        // tenant names and history) and left any portal login pointing at nothing,
        // so that login just got "Access denied". Explain instead.
        BlockedReason = await BlockedReasonAsync(entity.Id);
        if (BlockedReason is not null)
        {
            Tenant = entity;
            return Page();
        }
        _db.Tenants.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task<string?> BlockedReasonAsync(int id)
    {
        var leases = await _db.Leases.CountAsync(l => l.Tenants.Any(t => t.Id == id));
        var login = await _db.Users.AnyAsync(u => u.TenantId == id);
        var parts = new List<string>();
        if (leases > 0) parts.Add($"they're on {leases} lease(s) (edit the lease to remove them, or keep them for its history)");
        if (login) parts.Add("they have a tenant portal login (remove it under Settings > Users)");
        return parts.Count == 0 ? null : "This tenant can't be deleted: " + string.Join(", and ", parts) + ".";
    }
}
