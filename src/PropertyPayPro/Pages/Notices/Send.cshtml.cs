using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.ScheduledTasks;

namespace PropertyPayPro.Pages.Notices;

/// <summary>
/// Compose and send a notice "package" — a cover email with one or
/// more PDF attachments — to one or more tenants on the notice's lease.
///
/// Admin picks which lease documents to attach (the notice is pre-
/// checked), which tenants to send to (all with email on file are
/// pre-checked), and a cover email template that pre-fills Subject +
/// Body. Setting Send-at to a future time enqueues a ScheduledTask
/// instead of sending immediately; the background runner picks it up
/// and runs the same code path at the right time.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole)]
public class SendModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly MailService _mail;
    private readonly IAuditLog _audit;
    private readonly NoticeTemplateService _tpl;

    public SendModel(
        ApplicationDbContext db,
        IDocumentStorage storage,
        MailService mail,
        IAuditLog audit,
        NoticeTemplateService tpl)
    {
        _db = db;
        _storage = storage;
        _mail = mail;
        _audit = audit;
        _tpl = tpl;
    }

    public class InputModel
    {
        public int NoticeId { get; set; }

        [Required, StringLength(300)]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = "";

        [Required, StringLength(16000)]
        [Display(Name = "Cover body")]
        public string Body { get; set; } = "";

        [Display(Name = "Template")]
        public int? TemplateId { get; set; }

        public List<int> AttachmentIds { get; set; } = new();
        public List<string> Recipients { get; set; } = new();

        /// <summary>
        /// Optional future-dated send. Interpreted as the admin's
        /// local time (datetime-local input) and converted to UTC
        /// before enqueueing. Blank / in the past = send now.
        /// </summary>
        [Display(Name = "Send at")]
        [DataType(DataType.DateTime)]
        public DateTime? SendAtLocal { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public LeaseDocument? Notice { get; private set; }
    public List<LeaseDocument> AvailableAttachments { get; private set; } = new();
    public List<Tenant> AvailableRecipients { get; private set; } = new();
    public List<EmailLog> RecentDeliveries { get; private set; } = new();
    public List<ScheduledTask> PendingScheduled { get; private set; } = new();
    public SelectList TemplateOptions { get; private set; } = default!;
    public bool MailConfigured => _mail.IsConfigured;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();

        Input.NoticeId = Notice!.Id;
        Input.AttachmentIds = new List<int> { Notice.Id };
        Input.Recipients = AvailableRecipients
            .Where(t => !string.IsNullOrWhiteSpace(t.Email))
            .Select(t => t.Email!.Trim())
            .ToList();

        await SeedFromTemplateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostReloadTemplateAsync()
    {
        if (!await LoadAsync(Input.NoticeId)) return NotFound();
        ModelState.Clear();
        await SeedFromTemplateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync()
    {
        if (!await LoadAsync(Input.NoticeId)) return NotFound();

        if (!_mail.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, "SMTP is not configured — cannot send.");
            return Page();
        }
        if (!ModelState.IsValid) return Page();
        if (Input.AttachmentIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Attach at least one document.");
            return Page();
        }
        if (Input.Recipients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Pick at least one recipient.");
            return Page();
        }

        var allowedIds = AvailableAttachments.Select(a => a.Id).ToHashSet();
        allowedIds.Add(Notice!.Id);
        var chosenIds = await _db.LeaseDocuments
            .Where(d => Input.AttachmentIds.Contains(d.Id) && allowedIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync();

        // Translate the admin's local time to UTC. The browser sends
        // the datetime-local as "unspecified"; we treat it as the
        // server's local kind to stay consistent across the app.
        var sendAtUtc = Input.SendAtLocal.HasValue
            ? DateTime.SpecifyKind(Input.SendAtLocal.Value, DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;

        // Future dates enqueue a task; blank or past = immediate send.
        if (sendAtUtc.HasValue && sendAtUtc.Value > DateTime.UtcNow.AddMinutes(1))
        {
            await EnqueueAsync(chosenIds, sendAtUtc.Value);
            return RedirectToPage("Index");
        }

        return await SendNowAsync(chosenIds);
    }

    private async Task<IActionResult> SendNowAsync(List<int> chosenIds)
    {
        var chosen = await _db.LeaseDocuments
            .Where(d => chosenIds.Contains(d.Id))
            .ToListAsync();

        var attachments = new List<EmailAttachment>();
        foreach (var d in chosen)
        {
            await using var stream = await _storage.OpenReadAsync(d.StorageKey);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            attachments.Add(new EmailAttachment(d.FileName, d.ContentType, ms.ToArray()));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _mail.SendNoticePackageAsync(
            baseUrl, Notice!.LeaseId,
            Input.Recipients, Input.Subject, Input.Body,
            attachments);

        if (result.Sent > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var d in chosen)
            {
                d.SentCount += result.Sent;
                d.LastSentUtc = now;
            }
            await _db.SaveChangesAsync();
        }

        await _audit.WriteAsync("NoticeSent",
            entityType: nameof(LeaseDocument),
            entityId: Notice!.Id,
            details: $"To {result.Sent}/{result.TotalRecipients}; subject=\"{Input.Subject}\"; attachments={chosenIds.Count}");

        TempData["Message"] = $"Notice package sent to {result.Sent} recipient(s)"
            + (result.Failed > 0 ? $"; {result.Failed} failed." : ".");
        if (result.Failed > 0 && result.Errors.Any())
        {
            TempData["Error"] = "Failures: " + string.Join(" | ", result.Errors.Take(5));
        }
        return RedirectToPage("Index");
    }

    private async Task EnqueueAsync(List<int> chosenIds, DateTime sendAtUtc)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var payload = new NoticeSendPayload(
            LeaseId: Notice!.LeaseId,
            PrimaryNoticeId: Notice.Id,
            AttachmentIds: chosenIds.ToArray(),
            Recipients: Input.Recipients.ToArray(),
            Subject: Input.Subject,
            Body: Input.Body,
            BaseUrl: baseUrl);

        var task = new ScheduledTask
        {
            Kind = ScheduledTaskKind.NoticeSend,
            Status = ScheduledTaskStatus.Pending,
            RunAtUtc = sendAtUtc,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Description = $"Notice \"{Input.Subject}\" to {Input.Recipients.Count} recipient(s)",
        };
        _db.ScheduledTasks.Add(task);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("NoticeScheduled",
            entityType: nameof(LeaseDocument),
            entityId: Notice!.Id,
            details: $"RunAtUtc={sendAtUtc:O}; subject=\"{Input.Subject}\"; recipients={Input.Recipients.Count}");

        TempData["Message"] = $"Scheduled send for {sendAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} to {Input.Recipients.Count} recipient(s).";
    }

    private async Task<bool> LoadAsync(int noticeId)
    {
        Notice = await _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .FirstOrDefaultAsync(d => d.Id == noticeId && d.Type == LeaseDocumentType.Notice);
        if (Notice is null) return false;

        AvailableAttachments = await _db.LeaseDocuments
            .Where(d => d.LeaseId == Notice.LeaseId && d.Id != Notice.Id)
            .OrderByDescending(d => d.UploadedOn)
            .ToListAsync();

        AvailableRecipients = Notice.Lease!.Tenants
            .Where(t => !string.IsNullOrWhiteSpace(t.Email))
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .ToList();

        RecentDeliveries = await _db.EmailLogs
            .Where(l => l.LeaseId == Notice.LeaseId && l.Kind == EmailKind.Notice)
            .OrderByDescending(l => l.CreatedUtc)
            .Take(10)
            .ToListAsync();

        // Pending/running scheduled sends for this notice, newest first.
        // Matched by the primary notice id embedded in the payload JSON.
        var noticeIdNeedle = $"\"PrimaryNoticeId\":{Notice.Id}";
        PendingScheduled = await _db.ScheduledTasks
            .Where(t => t.Kind == ScheduledTaskKind.NoticeSend
                && (t.Status == ScheduledTaskStatus.Pending || t.Status == ScheduledTaskStatus.Running)
                && t.PayloadJson.Contains(noticeIdNeedle))
            .OrderBy(t => t.RunAtUtc)
            .ToListAsync();

        var templates = await _tpl.ListAsync(NoticeTemplateScope.CoverEmail);
        TemplateOptions = new SelectList(
            templates.Select(t => new { Value = t.Id, Text = t.Label }),
            "Value", "Text");

        // Default to the first cover-email template on first GET.
        if (!Input.TemplateId.HasValue && templates.Count > 0)
            Input.TemplateId = templates[0].Id;

        return true;
    }

    private async Task SeedFromTemplateAsync()
    {
        if (Notice is null || !Input.TemplateId.HasValue) return;
        var template = await _tpl.GetAsync(Input.TemplateId.Value);
        if (template is null) return;

        var rendered = _tpl.Render(template, new TemplateContext(
            Lease: Notice.Lease,
            NoticeTitle: Notice.SourceTitle ?? Notice.Description ?? Notice.FileName));
        Input.Subject = rendered.TitleOrSubject;
        Input.Body = rendered.Body;
    }

    public async Task<IActionResult> OnPostCancelScheduledAsync(int taskId)
    {
        var task = await _db.ScheduledTasks.FirstOrDefaultAsync(t =>
            t.Id == taskId && t.Status == ScheduledTaskStatus.Pending);
        if (task is null)
        {
            TempData["Error"] = "That scheduled send is no longer pending.";
            return RedirectToPage(new { id = Input.NoticeId });
        }
        task.Status = ScheduledTaskStatus.Cancelled;
        task.CompletedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync("ScheduledTaskCancelled",
            entityType: nameof(ScheduledTask), entityId: task.Id,
            details: task.Description);
        TempData["Message"] = "Scheduled send cancelled.";
        return RedirectToPage(new { id = Input.NoticeId });
    }
}
