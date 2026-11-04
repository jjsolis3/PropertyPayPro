using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Property Property { get; set; } = new();

    /// <summary>Why this property can't be deleted, or null when it can.</summary>
    public string? BlockedReason { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Properties.FindAsync(id);
        if (entity is null) return NotFound();
        Property = entity;
        BlockedReason = await BlockedReasonAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Properties.FindAsync(Property.Id);
        if (entity is null) return RedirectToPage("Index");
        // Leases, expenses and service tickets reference the property (Restrict), so
        // deleting it would fail in the database. Explain instead of crashing.
        BlockedReason = await BlockedReasonAsync(entity.Id);
        if (BlockedReason is not null)
        {
            Property = entity;
            return Page();
        }
        _db.Properties.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task<string?> BlockedReasonAsync(int id)
    {
        var leases = await _db.Leases.CountAsync(l => l.PropertyId == id);
        var expenses = await _db.PropertyExpenses.CountAsync(e => e.PropertyId == id);
        var tickets = await _db.ServiceTickets.CountAsync(t => t.PropertyId == id);
        var parts = new List<string>();
        if (leases > 0) parts.Add($"{leases} lease(s)");
        if (expenses > 0) parts.Add($"{expenses} expense(s)");
        if (tickets > 0) parts.Add($"{tickets} service ticket(s)");
        return parts.Count == 0 ? null
            : $"This property still has {string.Join(", ", parts)}. Delete or move those first; deleting the property would erase their history.";
    }
}
