using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

public class PaymentsModel : PortalPageBase
{
    public PaymentsModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
        : base(db, users) { }

    public List<RentPayment> Payments { get; private set; } = new();
    public decimal LifetimePaid { get; private set; }
    public decimal PaidThisYear { get; private set; }
    public int PaymentCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        Payments = await Db.RentPayments
            .Where(p => p.PaidByTenantId == CurrentTenant!.Id)
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .ToListAsync();

        LifetimePaid = Payments.Sum(p => p.Amount);
        PaymentCount = Payments.Count;

        var yearStart = new DateOnly(DateTime.UtcNow.Year, 1, 1);
        PaidThisYear = Payments.Where(p => p.PaidOn >= yearStart).Sum(p => p.Amount);

        return Page();
    }
}
