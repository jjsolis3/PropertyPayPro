using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public class BillingService
{
    private readonly ApplicationDbContext _db;
    private readonly PdfService _pdf;

    public BillingService(ApplicationDbContext db, PdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    /// <summary>
    /// Creates the month's rent bill for every active lease that doesn't
    /// have one yet, due on the lease's own due day (clamped to the month's
    /// length). Returns the bills created.
    /// </summary>
    public async Task<List<RentalCharge>> GenerateChargesForPeriodAsync(int year, int month)
    {
        var periodStart = new DateOnly(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var periodEnd = new DateOnly(year, month, daysInMonth);

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

        var newCharges = new List<RentalCharge>();
        foreach (var lease in activeLeases)
        {
            if (existing.Contains(lease.Id)) continue;

            var charge = new RentalCharge
            {
                LeaseId = lease.Id,
                Kind = ChargeKind.Rent,
                BillingPeriodStart = periodStart,
                DueDate = new DateOnly(year, month, Math.Clamp(lease.RentDueDay, 1, daysInMonth)),
                AmountDue = lease.MonthlyRent
            };
            _db.RentalCharges.Add(charge);
            newCharges.Add(charge);
        }

        await _db.SaveChangesAsync();

        foreach (var c in newCharges)
        {
            try { await _pdf.GenerateBillAsync(c.Id); }
            catch { /* don't fail the whole generation if a PDF fails */ }
        }

        return newCharges;
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

    /// <summary>Applies a late fee to every current candidate. Returns the fees created.</summary>
    public async Task<List<RentalCharge>> ApplyAllLateFeesAsync()
    {
        var applied = new List<RentalCharge>();
        foreach (var candidate in await GetLateFeeCandidatesAsync())
        {
            var fee = await ApplyLateFeeAsync(candidate.Id);
            if (fee is not null) applied.Add(fee);
        }
        return applied;
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
