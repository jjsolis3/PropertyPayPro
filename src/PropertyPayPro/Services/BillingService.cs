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
        var periodEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var dueDate = new DateOnly(year, month, Math.Min(DueDayOfMonth, DateTime.DaysInMonth(year, month)));

        // A lease is billable for the period if it has started by the end of the period
        // AND either it's month-to-month (rolls forever) or its term overlaps the period.
        var activeLeases = await _db.Leases
            .Where(l => l.StartDate <= periodEnd
                && (l.IsMonthToMonth || l.EndDate >= periodStart))
            .ToListAsync();

        var existing = await _db.RentalCharges
            .Where(c => c.BillingPeriodStart == periodStart && c.Kind == ChargeKind.Rent)
            .Select(c => c.LeaseId)
            .ToListAsync();

        var created = 0;
        foreach (var lease in activeLeases)
        {
            if (existing.Contains(lease.Id)) continue;

            _db.RentalCharges.Add(new RentalCharge
            {
                LeaseId = lease.Id,
                Kind = ChargeKind.Rent,
                BillingPeriodStart = periodStart,
                DueDate = dueDate,
                AmountDue = lease.MonthlyRent
            });
            created++;
        }

        await _db.SaveChangesAsync();
        return created;
    }

    public async Task<List<RentalCharge>> GetLateFeeCandidatesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rentCharges = await _db.RentalCharges
            .Where(c => c.Kind == ChargeKind.Rent)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Allocations)
            .ToListAsync();

        var existingLateFees = await _db.RentalCharges
            .Where(c => c.Kind == ChargeKind.LateFee)
            .Select(c => new { c.LeaseId, c.BillingPeriodStart })
            .ToListAsync();
        var lateFeeKeys = existingLateFees
            .Select(x => (x.LeaseId, x.BillingPeriodStart))
            .ToHashSet();

        return rentCharges
            .Where(c => c.Balance > 0
                && c.Lease!.LateFeeAmount > 0
                && c.DueDate.AddDays(c.Lease.LateFeeGraceDays) < today
                && !lateFeeKeys.Contains((c.LeaseId, c.BillingPeriodStart)))
            .ToList();
    }

    public async Task<RentalCharge?> ApplyLateFeeAsync(int rentChargeId)
    {
        var rentCharge = await _db.RentalCharges
            .Include(c => c.Lease)
            .FirstOrDefaultAsync(c => c.Id == rentChargeId && c.Kind == ChargeKind.Rent);

        if (rentCharge?.Lease is null || rentCharge.Lease.LateFeeAmount <= 0) return null;

        var lateFee = new RentalCharge
        {
            LeaseId = rentCharge.LeaseId,
            Kind = ChargeKind.LateFee,
            BillingPeriodStart = rentCharge.BillingPeriodStart,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AmountDue = rentCharge.Lease.LateFeeAmount,
            Notes = $"Late fee applied for {rentCharge.BillingPeriodStart:MMMM yyyy} rent."
        };
        _db.RentalCharges.Add(lateFee);
        await _db.SaveChangesAsync();
        return lateFee;
    }

    public async Task<List<PaymentAllocation>> SuggestAllocationsAsync(int leaseId, decimal amount)
    {
        var outstanding = await _db.RentalCharges
            .Where(c => c.LeaseId == leaseId)
            .Include(c => c.Allocations)
            .OrderBy(c => c.BillingPeriodStart)
            .ThenBy(c => c.Kind)
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
