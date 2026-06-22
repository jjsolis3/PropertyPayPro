using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AppSettingsService _settings;

    public CreateModel(ApplicationDbContext db, AppSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    [BindProperty]
    public Lease Lease { get; set; } = new()
    {
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
    };

    [BindProperty]
    public List<int> SelectedTenantIds { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;
    public SelectList Tenants { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync()
    {
        var defaults = await _settings.GetAsync();
        Lease.RentDueDay = defaults.DefaultRentDueDay;
        Lease.LateFeeGraceDays = defaults.DefaultLateFeeGraceDays;
        Lease.LateFeeAmount = defaults.DefaultLateFeeAmount;
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

        var tenants = await _db.Tenants.Where(t => SelectedTenantIds.Contains(t.Id)).ToListAsync();
        Lease.Tenants = tenants;

        _db.Leases.Add(Lease);
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
