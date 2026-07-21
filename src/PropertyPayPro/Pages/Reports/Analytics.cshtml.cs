using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Reports;

/// <summary>
/// Portfolio analytics dashboard. All data comes from existing tables
/// — nothing needs to be persisted anew. The page is read-only and
/// available to both Admins and Managers via the /Reports folder
/// convention.
///
/// Sections rendered on the page (each backed by a property here):
///   • Portfolio KPIs: properties, active leases, occupancy, monthly
///     rent roll, on-time payment %.
///   • Trailing-12-month revenue + expense line chart (RevenueByMonth /
///     ExpensesByMonth).
///   • Per-property net cash flow bar chart for trailing 12 months
///     (PerPropertyNet).
///   • Expense breakdown by category for trailing 12 months
///     (ExpensesByCategory) — donut chart.
///   • Delinquency aging (0-30 / 31-60 / 61-90 / 90+) computed from
///     open RentalCharge balances (AgingBuckets).
///   • Rent-roll forecast for the next 3 / 6 / 12 months from active
///     leases (ForecastMonths / ForecastAmounts).
/// </summary>
[Authorize]
public class AnalyticsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public AnalyticsModel(ApplicationDbContext db) => _db = db;

    // Portfolio KPIs
    public int PropertyCount { get; private set; }
    public int ActiveLeaseCount { get; private set; }
    public int TotalLeaseCount { get; private set; }
    public decimal MonthlyRentRoll { get; private set; }
    public int OnTimePercent { get; private set; }
    public decimal Revenue12mo { get; private set; }
    public decimal Expenses12mo { get; private set; }
    public decimal Net12mo => Revenue12mo - Expenses12mo;

    // Charts — parallel lists so the chart component consumes them as arrays.
    public List<string> MonthLabels { get; private set; } = new();
    public List<decimal> RevenueByMonth { get; private set; } = new();
    public List<decimal> ExpensesByMonth { get; private set; } = new();

    // Per-property net (last 12 mo) — three parallel lists.
    public List<string> PropertyLabels { get; private set; } = new();
    public List<decimal> PropertyRevenue { get; private set; } = new();
    public List<decimal> PropertyExpenses { get; private set; } = new();

    // Expense breakdown by category (last 12 mo).
    public List<string> CategoryLabels { get; private set; } = new();
    public List<decimal> CategoryAmounts { get; private set; } = new();

    // Delinquency aging.
    public class AgingBucket
    {
        public string Label { get; init; } = "";
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
    public List<AgingBucket> AgingBuckets { get; private set; } = new();
    public decimal AgingTotal => AgingBuckets.Sum(b => b.Amount);
    public int AgingChargeCount => AgingBuckets.Sum(b => b.Count);

    // Forecast for the next N months from active leases at their
    // current monthly rent — a rough top-line projection; doesn't
    // model renewals, terminations, or rent changes.
    public decimal Forecast3mo { get; private set; }
    public decimal Forecast6mo { get; private set; }
    public decimal Forecast12mo { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);
        // 12-month window ends on last day of the current month, starts
        // on the 1st of that month 11 months ago.
        var trailingStart = startOfMonth.AddMonths(-11);
        var trailingEnd = startOfMonth.AddMonths(1).AddDays(-1);

        // Portfolio-level counts and rent roll (from active leases).
        PropertyCount = await _db.Properties.CountAsync();
        var leases = await _db.Leases.ToListAsync();
        TotalLeaseCount = leases.Count;
        var active = leases.Where(l => l.StartDate <= today
            && (l.IsMonthToMonth || l.EndDate >= today)).ToList();
        ActiveLeaseCount = active.Count;
        MonthlyRentRoll = active.Sum(l => l.MonthlyRent);

        // Trailing-12-month revenue (RentPayments.PaidOn in window).
        var payments = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Where(p => p.PaidOn >= trailingStart && p.PaidOn <= trailingEnd)
            .ToListAsync();
        Revenue12mo = payments.Sum(p => p.Amount);

        // Trailing-12-month expenses (PropertyExpenses paid in window).
        var expenses = await _db.PropertyExpenses
            .Include(e => e.Property)
            .Where(e => e.PaidOn.HasValue
                && e.PaidOn.Value >= trailingStart
                && e.PaidOn.Value <= trailingEnd)
            .ToListAsync();
        Expenses12mo = expenses.Sum(e => e.AmountDue);

        // Build 12 monthly buckets in chronological order.
        for (var i = 0; i < 12; i++)
        {
            var monthStart = trailingStart.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            MonthLabels.Add(monthStart.ToString("MMM yy"));
            RevenueByMonth.Add(payments
                .Where(p => p.PaidOn >= monthStart && p.PaidOn <= monthEnd)
                .Sum(p => p.Amount));
            ExpensesByMonth.Add(expenses
                .Where(e => e.PaidOn.HasValue
                    && e.PaidOn.Value >= monthStart
                    && e.PaidOn.Value <= monthEnd)
                .Sum(e => e.AmountDue));
        }

        // Per-property revenue + expenses (last 12 mo).
        var properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();
        foreach (var p in properties)
        {
            var rev = payments
                .Where(pay => pay.Lease?.PropertyId == p.Id)
                .Sum(pay => pay.Amount);
            var exp = expenses
                .Where(e => e.PropertyId == p.Id)
                .Sum(e => e.AmountDue);
            PropertyLabels.Add(p.Name);
            PropertyRevenue.Add(rev);
            PropertyExpenses.Add(exp);
        }

        // Expense breakdown by category (last 12 mo).
        foreach (var g in expenses.GroupBy(e => e.Category).OrderByDescending(g => g.Sum(e => e.AmountDue)))
        {
            CategoryLabels.Add(g.Key.ToString());
            CategoryAmounts.Add(g.Sum(e => e.AmountDue));
        }

        // Delinquency aging — every past-due RentalCharge that still has
        // a balance, bucketed by how far past-due it is.
        var openCharges = await _db.RentalCharges
            .Include(c => c.Allocations)
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Where(c => c.DueDate < today)
            .ToListAsync();
        var pastDue = openCharges.Where(c => c.Balance > 0).ToList();
        AgingBuckets = new List<AgingBucket>
        {
            new() { Label = "0–30 days" },
            new() { Label = "31–60 days" },
            new() { Label = "61–90 days" },
            new() { Label = "90+ days" }
        };
        foreach (var c in pastDue)
        {
            var daysPast = today.DayNumber - c.DueDate.DayNumber;
            var idx = daysPast <= 30 ? 0 : daysPast <= 60 ? 1 : daysPast <= 90 ? 2 : 3;
            AgingBuckets[idx].Count++;
            AgingBuckets[idx].Amount += c.Balance;
        }

        // On-time %: for every fully-paid RentalCharge whose due date
        // fell in the last 365 days, check whether the last allocated
        // payment happened on or before the due date.
        var recentPaidCharges = await _db.RentalCharges
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .Where(c => c.DueDate >= today.AddDays(-365) && c.DueDate <= today)
            .ToListAsync();
        var onTimeCandidates = recentPaidCharges
            .Where(c => c.Allocations.Any() && c.Balance <= 0m)
            .ToList();
        if (onTimeCandidates.Count > 0)
        {
            var onTime = onTimeCandidates.Count(c =>
            {
                var lastPaidOn = c.Allocations
                    .Where(a => a.Payment != null)
                    .Max(a => (DateOnly?)a.Payment!.PaidOn);
                return lastPaidOn.HasValue && lastPaidOn.Value <= c.DueDate;
            });
            OnTimePercent = (int)Math.Round(onTime * 100.0 / onTimeCandidates.Count);
        }

        // Forecast: active-lease monthly rent × N months.
        Forecast3mo = MonthlyRentRoll * 3;
        Forecast6mo = MonthlyRentRoll * 6;
        Forecast12mo = MonthlyRentRoll * 12;
    }
}
