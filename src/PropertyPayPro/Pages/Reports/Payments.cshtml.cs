using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class PaymentsReportModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public PaymentsReportModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public DateOnly? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? LeaseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PropertyId { get; set; }

    [BindProperty(SupportsGet = true)]
    public PaymentMethod? Method { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TenantSearch { get; set; }

    public List<Property> PropertyOptions { get; private set; } = new();
    public List<Lease> LeaseOptions { get; private set; } = new();
    public List<RentPayment> Payments { get; private set; } = new();

    public decimal TotalCollected { get; private set; }
    public int PaymentCount => Payments.Count;
    public decimal AveragePayment => PaymentCount == 0 ? 0 : TotalCollected / PaymentCount;

    public List<TenantRollup> ByTenant { get; private set; } = new();
    public List<MonthRollup> ByMonth { get; private set; } = new();

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
        LeaseOptions = await _db.Leases
            .Include(l => l.Property).Include(l => l.Tenants)
            .OrderBy(l => l.Property!.Name).ToListAsync();

        var q = _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenants)
            .AsQueryable();

        q = q.Where(p => p.PaidOn >= FromDate && p.PaidOn <= ToDate);
        if (LeaseId.HasValue) q = q.Where(p => p.LeaseId == LeaseId.Value);
        if (PropertyId.HasValue) q = q.Where(p => p.Lease!.PropertyId == PropertyId.Value);
        if (Method.HasValue) q = q.Where(p => p.Method == Method.Value);

        Payments = await q.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(TenantSearch))
        {
            var term = TenantSearch.Trim().ToLowerInvariant();
            Payments = Payments
                .Where(p => (p.Reference ?? "").ToLowerInvariant().Contains(term)
                         || p.Lease!.Tenants.Any(t => t.DisplayName.ToLowerInvariant().Contains(term)))
                .ToList();
        }

        TotalCollected = Payments.Sum(p => p.Amount);

        // Rollup "paid by" derived from Reference text ("from X") or lease tenant names
        ByTenant = Payments
            .GroupBy(p => InferTenantName(p))
            .Select(g => new TenantRollup
            {
                TenantName = g.Key,
                Count = g.Count(),
                Total = g.Sum(p => p.Amount)
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        ByMonth = Payments
            .GroupBy(p => new DateOnly(p.PaidOn.Year, p.PaidOn.Month, 1))
            .Select(g => new MonthRollup
            {
                Month = g.Key,
                Count = g.Count(),
                Total = g.Sum(p => p.Amount)
            })
            .OrderByDescending(r => r.Month)
            .ToList();
    }

    private static string InferTenantName(RentPayment p)
    {
        // Pattern from import: "Zelle (WF Deposit) from Adalberto Cruz"
        var r = p.Reference ?? "";
        var idx = r.LastIndexOf(" from ", StringComparison.OrdinalIgnoreCase);
        if (idx > 0) return r[(idx + 6)..].Trim();
        // Fallback: lease tenant names
        return p.Lease?.TenantNames ?? "(unknown)";
    }

    private FileResult ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Property,Tenant(s),Paid by,Method,Reference,Amount");
        foreach (var p in Payments)
        {
            sb.AppendLine(string.Join(",",
                p.PaidOn.ToString("yyyy-MM-dd"),
                Q(p.Lease?.Property?.Name),
                Q(p.Lease?.TenantNames),
                Q(InferTenantName(p)),
                p.Method,
                Q(p.Reference),
                p.Amount.ToString("F2")));
        }
        sb.AppendLine();
        sb.AppendLine($",,,,,Total,{TotalCollected:F2}");
        var filename = $"payments-{FromDate:yyyyMMdd}-{ToDate:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
    }

    private static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    public class TenantRollup
    {
        public string TenantName { get; set; } = "";
        public int Count { get; set; }
        public decimal Total { get; set; }
    }

    public class MonthRollup
    {
        public DateOnly Month { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }
    }
}
