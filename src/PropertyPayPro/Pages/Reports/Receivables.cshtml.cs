using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class ReceivablesModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public ReceivablesModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public DateOnly? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PropertyId { get; set; }

    public List<Property> PropertyOptions { get; private set; } = new();

    public decimal TotalBilled { get; private set; }
    public decimal TotalCollected { get; private set; }
    public decimal TotalOutstanding { get; private set; }
    public decimal OnTimePercent { get; private set; }

    public AgingBuckets Aging { get; private set; } = new();
    public List<LeaseRow> LeaseRows { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string? export)
    {
        await LoadAsync();
        if (export == "csv") return ExportCsv();
        return Page();
    }

    private async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        FromDate ??= new DateOnly(today.Year, 1, 1);
        ToDate ??= today;

        PropertyOptions = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        var charges = await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        if (PropertyId.HasValue)
        {
            charges = charges.Where(c => c.Lease!.PropertyId == PropertyId.Value).ToList();
        }

        var inRange = charges
            .Where(c => c.DueDate >= FromDate && c.DueDate <= ToDate)
            .ToList();

        TotalBilled = inRange.Sum(c => c.AmountDue);
        TotalCollected = inRange.Sum(c => c.AmountPaid);
        TotalOutstanding = charges.Sum(c => c.Balance);

        var dueWithStatus = inRange.Where(c => c.AmountPaid >= c.AmountDue).ToList();
        if (dueWithStatus.Count > 0)
        {
            var onTime = dueWithStatus.Count(c =>
                c.Allocations.Count == 0 ||
                c.Allocations.Max(a => a.Payment?.PaidOn) <= c.DueDate);
            OnTimePercent = Math.Round(onTime * 100m / dueWithStatus.Count, 1);
        }

        Aging = BuildAging(charges, today);
        LeaseRows = charges
            .GroupBy(c => c.LeaseId)
            .Select(g => new LeaseRow
            {
                LeaseId = g.Key,
                PropertyName = g.First().Lease!.Property!.Name,
                TenantNames = g.First().Lease!.TenantNames,
                Billed = g.Where(c => c.DueDate >= FromDate && c.DueDate <= ToDate).Sum(c => c.AmountDue),
                Collected = g.Where(c => c.DueDate >= FromDate && c.DueDate <= ToDate).Sum(c => c.AmountPaid),
                Outstanding = g.Sum(c => c.Balance)
            })
            .Where(r => r.Billed > 0 || r.Outstanding > 0)
            .OrderByDescending(r => r.Outstanding)
            .ToList();
    }

    private static AgingBuckets BuildAging(IEnumerable<RentalCharge> charges, DateOnly today)
    {
        var b = new AgingBuckets();
        foreach (var c in charges)
        {
            if (c.Balance <= 0) continue;
            var daysOverdue = today.DayNumber - c.DueDate.DayNumber;
            if (daysOverdue <= 0) b.Current += c.Balance;
            else if (daysOverdue <= 30) b.D1_30 += c.Balance;
            else if (daysOverdue <= 60) b.D31_60 += c.Balance;
            else if (daysOverdue <= 90) b.D61_90 += c.Balance;
            else b.D90Plus += c.Balance;
        }
        return b;
    }

    private FileResult ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Property,Tenants,Billed,Collected,Outstanding");
        foreach (var r in LeaseRows)
        {
            sb.AppendLine($"\"{Esc(r.PropertyName)}\",\"{Esc(r.TenantNames)}\",{r.Billed:F2},{r.Collected:F2},{r.Outstanding:F2}");
        }
        sb.AppendLine();
        sb.AppendLine($"Totals,,{LeaseRows.Sum(r => r.Billed):F2},{LeaseRows.Sum(r => r.Collected):F2},{LeaseRows.Sum(r => r.Outstanding):F2}");
        var filename = $"receivables-{FromDate:yyyyMMdd}-{ToDate:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
    }

    private static string Esc(string s) => (s ?? "").Replace("\"", "\"\"");

    public class AgingBuckets
    {
        public decimal Current { get; set; }
        public decimal D1_30 { get; set; }
        public decimal D31_60 { get; set; }
        public decimal D61_90 { get; set; }
        public decimal D90Plus { get; set; }
        public decimal Total => Current + D1_30 + D31_60 + D61_90 + D90Plus;
    }

    public class LeaseRow
    {
        public int LeaseId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string TenantNames { get; set; } = string.Empty;
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
        public decimal Outstanding { get; set; }
    }
}
