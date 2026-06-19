using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Payments;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public RentPayment Payment { get; set; } = new()
    {
        PaidOn = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    public SelectList Leases { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? leaseId)
    {
        if (leaseId is int id) Payment.LeaseId = id;
        await LoadSelectListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSelectListAsync();
            return Page();
        }

        _db.RentPayments.Add(Payment);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadSelectListAsync()
    {
        var leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();

        Leases = new SelectList(
            leases.Select(l => new { l.Id, Label = $"{l.Tenant!.DisplayName} — {l.Property!.Name}" }),
            "Id", "Label");
    }
}
