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
    public MultiSelectList Tenants { get; private set; } = default!;

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
        ValidateEndDate();
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
        // MultiSelectList (not SelectList) so every tenant already on the lease is preselected.
        Tenants = new MultiSelectList(tenants.Select(t => new { t.Id, Name = t.DisplayName }), "Id", "Name", SelectedTenantIds);
    }

    /// <summary>
    /// Month-to-month leases ignore the end date everywhere, so a blank one is fine:
    /// store the start date as a placeholder. Fixed-term leases need a real end date
    /// after the start.
    /// </summary>
    private void ValidateEndDate()
    {
        var blank = ModelState.TryGetValue("Lease.EndDate", out var entry) && string.IsNullOrWhiteSpace(entry.AttemptedValue);
        if (Lease.IsMonthToMonth)
        {
            ModelState.Remove("Lease.EndDate");
            if (blank || Lease.EndDate == default) Lease.EndDate = Lease.StartDate;
            return;
        }
        if (blank)
        {
            ModelState.Remove("Lease.EndDate");
            ModelState.AddModelError("Lease.EndDate", "Enter an end date, or tick Month-to-month.");
        }
        else if (Lease.EndDate < Lease.StartDate)
        {
            ModelState.AddModelError("Lease.EndDate", "End date must be on or after the start date.");
        }
    }
}
