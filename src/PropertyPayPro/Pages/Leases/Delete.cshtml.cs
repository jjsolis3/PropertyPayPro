using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Lease Lease { get; set; } = new();

    public int PaymentCount { get; private set; }
    public int BillCount { get; private set; }
    public int DocumentCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (entity is null) return NotFound();
        Lease = entity;
        await CountAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .FirstOrDefaultAsync(l => l.Id == Lease.Id);
        if (entity is null) return RedirectToPage("Index");
        // Deleting a lease cascades to its bills, payments and documents. Refuse when
        // payments exist: that would silently erase the payment history (and the
        // allocation -> bill link is Restrict, so it can also fail in the database).
        await CountAsync(entity.Id);
        if (PaymentCount > 0)
        {
            Lease = entity;
            return Page();
        }
        _db.Leases.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task CountAsync(int leaseId)
    {
        PaymentCount = await _db.RentPayments.CountAsync(p => p.LeaseId == leaseId);
        BillCount = await _db.RentalCharges.CountAsync(c => c.LeaseId == leaseId);
        DocumentCount = await _db.LeaseDocuments.CountAsync(d => d.LeaseId == leaseId);
    }
}
