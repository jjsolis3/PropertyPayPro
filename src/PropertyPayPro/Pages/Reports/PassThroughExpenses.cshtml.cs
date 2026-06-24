using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class PassThroughExpensesModel : PageModel
{
    public enum FilterStatus { All, Reimbursed, NotReimbursed, Partial }

    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;

    public PassThroughExpensesModel(ApplicationDbContext db, MailService mail)
    {
        _db = db;
        _mail = mail;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PropertyId { get; set; }

    [BindProperty(SupportsGet = true)]
    public FilterStatus Status { get; set; } = FilterStatus.NotReimbursed;

    [BindProperty(SupportsGet = true)]
    public ExpenseCategory? Category { get; set; }

    public List<Property> PropertyOptions { get; private set; } = new();
    public List<PropertyExpense> Expenses { get; private set; } = new();

    public decimal TotalExpense { get; private set; }
    public decimal TotalReimbursed { get; private set; }
    public decimal TotalOutstanding { get; private set; }
    public bool MailConfigured => _mail.IsConfigured;

    public async Task<IActionResult> OnGetAsync(string? export)
    {
        await LoadAsync();
        if (export == "csv") return ExportCsv();
        return Page();
    }

    public async Task<IActionResult> OnPostSendRemindersAsync(int? leaseId)
    {
        if (!_mail.IsConfigured)
        {
            TempData["Error"] = "SMTP is not configured.";
            return RedirectToPage(GetRouteValues());
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        if (leaseId.HasValue)
        {
            // Single lease
            try
            {
                var log = await _mail.SendReimbursementReminderAsync(baseUrl, leaseId.Value);
                TempData["Message"] = log.Status == EmailStatus.Sent
                    ? $"Reminder sent to {log.ToAddress}."
                    : $"Skipped: {log.Error}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Send failed: {ex.Message}";
            }
        }
        else
        {
            // Bulk — every lease where the lease's property has unreimbursed pass-through expenses
            var leases = await _db.Leases
                .Include(l => l.Tenants)
                .Where(l => l.Tenants.Any(t => t.ReceiveReminders))
                .ToListAsync();

            var expenses = await _db.PropertyExpenses
                .Where(e => e.PassThroughToTenant)
                .ToListAsync();
            var propertiesWithOutstanding = expenses
                .Where(e => e.OutstandingReimbursement > 0)
                .Select(e => e.PropertyId)
                .Distinct()
                .ToHashSet();

            int sent = 0, skipped = 0, failed = 0;
            foreach (var lease in leases.Where(l => propertiesWithOutstanding.Contains(l.PropertyId)))
            {
                try
                {
                    var log = await _mail.SendReimbursementReminderAsync(baseUrl, lease.Id);
                    if (log.Status == EmailStatus.Sent) sent++; else skipped++;
                }
                catch { failed++; }
            }
            TempData["Message"] = $"Reminders: {sent} sent, {skipped} skipped, {failed} failed.";
        }
        return RedirectToPage(GetRouteValues());
    }

    private object GetRouteValues() => new
    {
        FromDate, ToDate, PropertyId, Status, Category
    };

    private async Task LoadAsync()
    {
        PropertyOptions = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        var q = _db.PropertyExpenses
            .Include(e => e.Property)
            .Where(e => e.PassThroughToTenant)
            .AsQueryable();

        if (FromDate.HasValue) q = q.Where(e => e.DueDate >= FromDate);
        if (ToDate.HasValue) q = q.Where(e => e.DueDate <= ToDate);
        if (PropertyId.HasValue) q = q.Where(e => e.PropertyId == PropertyId.Value);
        if (Category.HasValue) q = q.Where(e => e.Category == Category.Value);

        var all = await q.OrderByDescending(e => e.DueDate).ThenByDescending(e => e.Id).ToListAsync();

        Expenses = Status switch
        {
            FilterStatus.Reimbursed => all.Where(e => e.IsReimbursed && e.OutstandingReimbursement == 0).ToList(),
            FilterStatus.NotReimbursed => all.Where(e => !e.IsReimbursed).ToList(),
            FilterStatus.Partial => all.Where(e => e.IsReimbursed && e.OutstandingReimbursement > 0).ToList(),
            _ => all
        };

        TotalExpense = Expenses.Sum(e => e.AmountDue);
        TotalReimbursed = Expenses.Sum(e => e.ReimbursedAmount ?? 0);
        TotalOutstanding = Expenses.Sum(e => e.OutstandingReimbursement);
    }

    private FileResult ExportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Property,Category,Vendor,Description,Due,Paid,Amount,ReimbursedOn,ReimbursedAmount,Outstanding");
        foreach (var e in Expenses)
        {
            sb.AppendLine(string.Join(",",
                Q(e.Property?.Name),
                e.Category,
                Q(e.Vendor),
                Q(e.Description),
                e.DueDate?.ToString("yyyy-MM-dd") ?? "",
                e.PaidOn?.ToString("yyyy-MM-dd") ?? "",
                e.AmountDue.ToString("F2"),
                e.ReimbursedOn?.ToString("yyyy-MM-dd") ?? "",
                (e.ReimbursedAmount ?? 0).ToString("F2"),
                e.OutstandingReimbursement.ToString("F2")));
        }
        sb.AppendLine();
        sb.AppendLine($",,,,,,Total expense:,{TotalExpense:F2}");
        sb.AppendLine($",,,,,,Total reimbursed:,{TotalReimbursed:F2}");
        sb.AppendLine($",,,,,,Outstanding:,{TotalOutstanding:F2}");
        var filename = $"passthrough-expenses-{DateTime.UtcNow:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", filename);
    }

    private static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}
