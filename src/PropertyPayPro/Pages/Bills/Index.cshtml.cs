using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BillingService _billing;

    public IndexModel(ApplicationDbContext db, BillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    [BindProperty(SupportsGet = true)]
    public ChargeStatus? StatusFilter { get; set; }

    [BindProperty]
    public int GenerateYear { get; set; } = DateTime.UtcNow.Year;

    [BindProperty]
    public int GenerateMonth { get; set; } = DateTime.UtcNow.Month;

    public IList<RentalCharge> Charges { get; private set; } = new List<RentalCharge>();

    public async Task OnGetAsync()
    {
        await LoadChargesAsync();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        if (GenerateMonth < 1 || GenerateMonth > 12)
        {
            TempData["GenerateError"] = "Month must be 1–12.";
            return RedirectToPage();
        }

        var created = await _billing.GenerateChargesForPeriodAsync(GenerateYear, GenerateMonth);
        var periodLabel = new DateOnly(GenerateYear, GenerateMonth, 1).ToString("MMMM yyyy");
        TempData["GenerateMessage"] = created == 0
            ? $"No new bills generated for {periodLabel} (existing bills were skipped)."
            : $"Generated {created} new bill(s) for {periodLabel}.";
        return RedirectToPage();
    }

    private async Task LoadChargesAsync()
    {
        var query = _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations)
            .OrderByDescending(c => c.BillingPeriodStart)
            .AsQueryable();

        var list = await query.ToListAsync();
        if (StatusFilter.HasValue)
        {
            list = list.Where(c => c.Status == StatusFilter.Value).ToList();
        }
        Charges = list;
    }
}
