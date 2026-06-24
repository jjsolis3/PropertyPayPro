using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class AnnualPnLModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public AnnualPnLModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty(SupportsGet = true)]
    public int? PropertyId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Compare { get; set; }

    public List<Property> PropertyOptions { get; private set; } = new();

    public PeriodData This { get; private set; } = new();
    public PeriodData? Prev { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? export)
    {
        PropertyOptions = await _db.Properties.OrderBy(p => p.Name).ToListAsync();
        This = await LoadPeriodAsync(Year);
        if (Compare) Prev = await LoadPeriodAsync(Year - 1);
        if (export == "csv") return ExportCsv();
        return Page();
    }

    private async Task<PeriodData> LoadPeriodAsync(int year)
    {
        var startDate = new DateOnly(year, 1, 1);
        var endDate = new DateOnly(year, 12, 31);

        // Income: rent collected (sum of allocations to Rent charges paid in this year)
        // Plus late fees collected (allocations to LateFee charges paid in this year)
        // Plus pass-through reimbursements received this year
        var allocsQuery = _db.PaymentAllocations
            .Include(a => a.Payment)
            .Include(a => a.RentalCharge)
            .Where(a => a.Payment != null
                     && a.Payment.PaidOn >= startDate
                     && a.Payment.PaidOn <= endDate);
        if (PropertyId.HasValue)
        {
            allocsQuery = allocsQuery
                .Include(a => a.Payment!).ThenInclude(p => p.Lease)
                .Where(a => a.Payment!.Lease!.PropertyId == PropertyId.Value);
        }
        var allocs = await allocsQuery.ToListAsync();

        var rentIncome = allocs.Where(a => a.RentalCharge!.Kind == ChargeKind.Rent).Sum(a => a.Amount);
        var lateFeeIncome = allocs.Where(a => a.RentalCharge!.Kind == ChargeKind.LateFee).Sum(a => a.Amount);

        var expensesQuery = _db.PropertyExpenses
            .Where(e => e.PaidOn.HasValue
                     && e.PaidOn.Value >= startDate
                     && e.PaidOn.Value <= endDate);
        if (PropertyId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.PropertyId == PropertyId.Value);
        var expenses = await expensesQuery.ToListAsync();

        var reimbursements = expenses
            .Where(e => e.PassThroughToTenant
                     && e.ReimbursedOn.HasValue
                     && e.ReimbursedOn.Value >= startDate
                     && e.ReimbursedOn.Value <= endDate)
            .Sum(e => e.ReimbursedAmount ?? 0);

        var ticketsQuery = _db.ServiceTickets
            .Where(t => t.ResolvedOn.HasValue
                     && t.ResolvedOn.Value >= startDate
                     && t.ResolvedOn.Value <= endDate
                     && t.Cost.HasValue);
        if (PropertyId.HasValue)
            ticketsQuery = ticketsQuery.Where(t => t.PropertyId == PropertyId.Value);
        var tickets = await ticketsQuery.ToListAsync();

        var expensesByCategory = expenses
            .GroupBy(e => e.Category)
            .Select(g => new CategoryRow(g.Key.ToString(), g.Sum(e => e.AmountDue)))
            .OrderByDescending(c => c.Amount)
            .ToList();
        var repairsTotal = tickets.Sum(t => t.Cost ?? 0);

        return new PeriodData
        {
            Year = year,
            RentIncome = rentIncome,
            LateFeeIncome = lateFeeIncome,
            ReimbursementIncome = reimbursements,
            ExpensesByCategory = expensesByCategory,
            RepairsExpense = repairsTotal
        };
    }

    private FileResult ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PropertyPayPro Annual P&L — {Year}{(PropertyId.HasValue ? $" — Property #{PropertyId}" : "")}");
        sb.AppendLine();
        sb.AppendLine("INCOME");
        sb.AppendLine($"Rent collected,{This.RentIncome:F2}");
        sb.AppendLine($"Late fees collected,{This.LateFeeIncome:F2}");
        sb.AppendLine($"Reimbursements,{This.ReimbursementIncome:F2}");
        sb.AppendLine($"Total income,{This.TotalIncome:F2}");
        sb.AppendLine();
        sb.AppendLine("EXPENSES");
        foreach (var c in This.ExpensesByCategory)
            sb.AppendLine($"{c.Label},{c.Amount:F2}");
        sb.AppendLine($"Repairs,{This.RepairsExpense:F2}");
        sb.AppendLine($"Total expenses,{This.TotalExpenses:F2}");
        sb.AppendLine();
        sb.AppendLine($"NET (Income − Expenses),{This.Net:F2}");

        var filename = $"pnl-{Year}{(PropertyId.HasValue ? $"-prop{PropertyId}" : "")}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
    }

    public class PeriodData
    {
        public int Year { get; set; }
        public decimal RentIncome { get; set; }
        public decimal LateFeeIncome { get; set; }
        public decimal ReimbursementIncome { get; set; }
        public List<CategoryRow> ExpensesByCategory { get; set; } = new();
        public decimal RepairsExpense { get; set; }

        public decimal TotalIncome => RentIncome + LateFeeIncome + ReimbursementIncome;
        public decimal TotalExpenses => ExpensesByCategory.Sum(c => c.Amount) + RepairsExpense;
        public decimal Net => TotalIncome - TotalExpenses;
    }

    public record CategoryRow(string Label, decimal Amount);
}
