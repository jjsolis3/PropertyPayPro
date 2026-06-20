using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public class BillingService
{
    public const int DueDayOfMonth = 15;

    private readonly ApplicationDbContext _db;

    public BillingService(ApplicationDbContext db) => _db = db;

    public async Task<int> GenerateChargesForPeriodAsync(int year, int month)
    {
        var periodStart = new DateOnly(year, month, 1);
        var dueDate = new DateOnly(year, month, Math.Min(DueDayOfMonth, DateTime.DaysInMonth(year, month)));

        var activeLeases = await _db.Leases
            .Where(l => l.StartDate <= periodStart && l.EndDate >= periodStart)
            .ToListAsync();

        var existing = await _db.RentalCharges
            .Where(c => c.BillingPeriodStart == periodStart)
            .Select(c => c.LeaseId)
            .ToListAsync();

        var created = 0;
        foreach (var lease in activeLeases)
        {
            if (existing.Contains(lease.Id)) continue;

            _db.RentalCharges.Add(new RentalCharge
            {
                LeaseId = lease.Id,
                BillingPeriodStart = periodStart,
                DueDate = dueDate,
                AmountDue = lease.MonthlyRent
            });
            created++;
        }

        await _db.SaveChangesAsync();
        return created;
    }

    public async Task<List<PaymentAllocation>> SuggestAllocationsAsync(int leaseId, decimal amount)
    {
        var outstanding = await _db.RentalCharges
            .Where(c => c.LeaseId == leaseId)
            .Include(c => c.Allocations)
            .OrderBy(c => c.BillingPeriodStart)
            .ToListAsync();

        var suggestions = new List<PaymentAllocation>();
        var remaining = amount;

        foreach (var charge in outstanding)
        {
            if (remaining <= 0) break;
            var balance = charge.Balance;
            if (balance <= 0) continue;

            var alloc = Math.Min(balance, remaining);
            suggestions.Add(new PaymentAllocation
            {
                RentalChargeId = charge.Id,
                Amount = alloc,
                RentalCharge = charge
            });
            remaining -= alloc;
        }

        return suggestions;
    }
}
