using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class UnpaidBillsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;

    public UnpaidBillsModel(ApplicationDbContext db, MailService mail)
    {
        _db = db;
        _mail = mail;
    }

    public List<LeaseGroup> Groups { get; private set; } = new();
    public bool MailConfigured => _mail.IsConfigured;

    public decimal GrandTotal => Groups.Sum(g => g.TotalBalance);
    public int TotalUnpaidBills => Groups.Sum(g => g.UnpaidBills.Count);
    public int EligibleForEmail => Groups.Count(g => g.HasEmailEligibleTenant);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSendAsync(List<int> selectedLeaseIds)
    {
        if (!_mail.IsConfigured)
        {
            TempData["Error"] = "SMTP is not configured.";
            return RedirectToPage();
        }
        if (selectedLeaseIds is null || selectedLeaseIds.Count == 0)
        {
            TempData["Error"] = "Select at least one tenant before sending.";
            return RedirectToPage();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        int sent = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        foreach (var leaseId in selectedLeaseIds.Distinct())
        {
            try
            {
                var log = await _mail.SendStatementAsync(baseUrl, leaseId);
                if (log.Status == EmailStatus.Sent) sent++;
                else { skipped++; errors.Add($"Lease #{leaseId}: {log.Error}"); }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Lease #{leaseId}: {ex.Message}");
            }
        }

        TempData["Message"] = $"Statements: {sent} sent, {skipped} skipped (no eligible tenant), {failed} failed.";
        if (errors.Count > 0)
        {
            TempData["Details"] = string.Join(" | ", errors.Take(5));
        }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var lastStatementByLease = await _db.EmailLogs
            .Where(e => e.Kind == EmailKind.Statement && e.Status == EmailStatus.Sent && e.LeaseId != null)
            .GroupBy(e => e.LeaseId!.Value)
            .Select(g => new { LeaseId = g.Key, LastSent = g.Max(e => e.CreatedUtc) })
            .ToDictionaryAsync(x => x.LeaseId, x => x.LastSent);

        foreach (var l in leases)
        {
            var unpaid = l.Charges.Where(c => c.Balance > 0).OrderBy(c => c.DueDate).ToList();
            if (unpaid.Count == 0) continue;

            var eligibleTenant = l.Tenants.FirstOrDefault(t =>
                t.ReceiveReminders && !string.IsNullOrWhiteSpace(t.Email));

            Groups.Add(new LeaseGroup
            {
                LeaseId = l.Id,
                PropertyName = l.Property!.Name,
                TenantNames = l.TenantNames,
                EligibleEmail = eligibleTenant?.Email,
                AnyTenantHasEmail = l.Tenants.Any(t => !string.IsNullOrWhiteSpace(t.Email)),
                AnyTenantOptedOut = l.Tenants.Any(t => !t.ReceiveReminders),
                UnpaidBills = unpaid,
                OldestDueDate = unpaid.Min(c => c.DueDate),
                MostOverdueDays = unpaid.Max(c => Math.Max(0, today.DayNumber - c.DueDate.DayNumber)),
                TotalBalance = unpaid.Sum(c => c.Balance),
                LastStatementSentUtc = lastStatementByLease.TryGetValue(l.Id, out var ts) ? ts : null
            });
        }

        Groups = Groups
            .OrderByDescending(g => g.MostOverdueDays)
            .ThenByDescending(g => g.TotalBalance)
            .ToList();
    }

    public class LeaseGroup
    {
        public int LeaseId { get; set; }
        public string PropertyName { get; set; } = "";
        public string TenantNames { get; set; } = "";
        public string? EligibleEmail { get; set; }
        public bool AnyTenantHasEmail { get; set; }
        public bool AnyTenantOptedOut { get; set; }
        public List<RentalCharge> UnpaidBills { get; set; } = new();
        public DateOnly OldestDueDate { get; set; }
        public int MostOverdueDays { get; set; }
        public decimal TotalBalance { get; set; }
        public DateTime? LastStatementSentUtc { get; set; }

        public bool HasEmailEligibleTenant => !string.IsNullOrWhiteSpace(EligibleEmail);
    }
}
