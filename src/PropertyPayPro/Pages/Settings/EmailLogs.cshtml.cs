using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

/// <summary>
/// Admin view of every outbound email the app has tried to send.
/// Rows are read-only except for one action: Resend, which replays
/// the stored body against the stored ToAddress. Attachments are
/// NOT re-materialized by resend — this covers plain cover-email
/// retries, and anything with attachments is better re-sent from
/// its origin page (e.g. /Notices/Send) so the attachment picker
/// is applied fresh.
/// </summary>
public class EmailLogsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly IAuditLog _audit;
    private const int PageSize = 100;

    public EmailLogsModel(ApplicationDbContext db, MailService mail, IAuditLog audit)
    {
        _db = db;
        _mail = mail;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)] public EmailKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public EmailStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;

    public List<EmailLog> Logs { get; private set; } = new();
    public int Total { get; private set; }
    public int SentTotal { get; private set; }
    public int FailedTotal { get; private set; }
    public bool HasPrev => Page > 1;
    public bool HasNext => Page * PageSize < Total;

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostResendAsync(int id)
    {
        var original = await _db.EmailLogs.FirstOrDefaultAsync(l => l.Id == id);
        if (original is null)
        {
            TempData["Error"] = "That log entry no longer exists.";
            return RedirectToPage();
        }
        if (string.IsNullOrEmpty(original.BodyHtml))
        {
            // Legacy row from before the BodyHtml column existed, or a
            // kind that was never persisted. Resend would be a no-op.
            TempData["Error"] = "Original body was not stored — cannot resend. "
                + "Re-send from the source page (e.g. the Notice's Send page).";
            return RedirectToPage(new { Kind, Status, Q, Page });
        }
        if (!_mail.IsConfigured)
        {
            TempData["Error"] = "SMTP is not configured — cannot send.";
            return RedirectToPage(new { Kind, Status, Q, Page });
        }

        var retry = await _mail.ResendAsync(original);
        await _audit.WriteAsync("EmailResent",
            entityType: nameof(EmailLog),
            entityId: original.Id,
            details: $"Replay to {original.ToAddress}; new log #{retry.Id}; status={retry.Status}");

        TempData["Message"] = retry.Status == EmailStatus.Sent
            ? $"Resent to {original.ToAddress}."
            : $"Resend failed: {retry.Error}";
        return RedirectToPage(new { Kind, Status, Q, Page });
    }

    private async Task LoadAsync()
    {
        ViewData["SettingsTab"] = "emaillogs";

        var q = _db.EmailLogs.AsQueryable();
        if (Kind.HasValue) q = q.Where(l => l.Kind == Kind.Value);
        if (Status.HasValue) q = q.Where(l => l.Status == Status.Value);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim().ToLower();
            q = q.Where(l =>
                l.ToAddress.ToLower().Contains(needle)
                || l.Subject.ToLower().Contains(needle));
        }

        Total = await q.CountAsync();
        SentTotal = await _db.EmailLogs.CountAsync(l => l.Status == EmailStatus.Sent);
        FailedTotal = await _db.EmailLogs.CountAsync(l => l.Status == EmailStatus.Failed);
        if (Page < 1) Page = 1;

        Logs = await q.OrderByDescending(l => l.CreatedUtc)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
