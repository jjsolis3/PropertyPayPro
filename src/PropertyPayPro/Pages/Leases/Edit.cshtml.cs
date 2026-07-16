using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Lease Lease { get; set; } = new();

    [BindProperty]
    public List<int> SelectedTenantIds { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;
    public SelectList Tenants { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Leases
            .Include(l => l.Tenants)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (entity is null) return NotFound();
        Lease = entity;
        SelectedTenantIds = entity.Tenants.Select(t => t.Id).ToList();
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (SelectedTenantIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Pick at least one tenant.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        var existing = await _db.Leases
            .Include(l => l.Tenants)
            .FirstOrDefaultAsync(l => l.Id == Lease.Id);
        if (existing is null) return NotFound();

        // Update scalar fields
        existing.PropertyId = Lease.PropertyId;
        existing.StartDate = Lease.StartDate;
        existing.EndDate = Lease.EndDate;
        existing.MonthlyRent = Lease.MonthlyRent;
        existing.SecurityDeposit = Lease.SecurityDeposit;
        existing.RentDueDay = Lease.RentDueDay;
        existing.IsMonthToMonth = Lease.IsMonthToMonth;
        existing.LateFeeAmount = Lease.LateFeeAmount;
        existing.LateFeeGraceDays = Lease.LateFeeGraceDays;
        existing.Notes = Lease.Notes;

        // Sync tenants
        existing.Tenants.Clear();
        var tenants = await _db.Tenants.Where(t => SelectedTenantIds.Contains(t.Id)).ToListAsync();
        foreach (var t in tenants) existing.Tenants.Add(t);

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadSelectListsAsync()
    {
        Properties = new SelectList(await _db.Properties.OrderBy(p => p.Name).ToListAsync(), "Id", "Name");
        var tenants = await _db.Tenants.OrderBy(t => t.LastName).ToListAsync();
        Tenants = new SelectList(tenants.Select(t => new { t.Id, Name = t.DisplayName }), "Id", "Name", SelectedTenantIds);
    }
}
