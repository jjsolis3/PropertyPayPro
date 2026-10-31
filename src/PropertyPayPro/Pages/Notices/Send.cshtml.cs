using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Notices;

/// <summary>
/// Compose and send a notice "package" — a cover email with one or
/// more PDF attachments — to one or more tenants on the notice's lease.
///
/// Admin picks which lease documents to attach (the notice is pre-
/// checked), which tenants to send to (all with email on file are
/// pre-checked), and a cover email template that pre-fills Subject +
/// Body. All three are editable before Send. Each recipient gets its
/// own EmailLog row. On success, the notice's SentCount and
/// LastSentUtc are updated so Index can show a "Sent" badge.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole)]
public class SendModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly MailService _mail;

    public SendModel(ApplicationDbContext db, IDocumentStorage storage, MailService mail)
    {
        _db = db;
        _storage = storage;
        _mail = mail;
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
        public NoticeEmailTemplateKind Template { get; set; } = NoticeEmailTemplateKind.FormalNoticeDelivery;

        /// <summary>Which LeaseDocument ids to attach.</summary>
        public List<int> AttachmentIds { get; set; } = new();

        /// <summary>Which tenant email addresses to send to.</summary>
        public List<string> Recipients { get; set; } = new();
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public LeaseDocument? Notice { get; private set; }
    public List<LeaseDocument> AvailableAttachments { get; private set; } = new();
    public List<Tenant> AvailableRecipients { get; private set; } = new();
    public List<EmailLog> RecentDeliveries { get; private set; } = new();
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

        SeedFromTemplate();
        return Page();
    }

    public async Task<IActionResult> OnPostReloadTemplateAsync()
    {
        if (!await LoadAsync(Input.NoticeId)) return NotFound();
        ModelState.Clear();
        SeedFromTemplate();
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

        // Pull the chosen attachments from storage. Any id the user
        // snuck in that's not on this lease is silently dropped.
        var allowedIds = AvailableAttachments.Select(a => a.Id).ToHashSet();
        allowedIds.Add(Notice!.Id);
        var chosen = await _db.LeaseDocuments
            .Where(d => Input.AttachmentIds.Contains(d.Id) && allowedIds.Contains(d.Id))
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
            // Bump the Sent counters on every doc that was in this package.
            var now = DateTime.UtcNow;
            foreach (var d in chosen)
            {
                d.SentCount += result.Sent;
                d.LastSentUtc = now;
            }
            await _db.SaveChangesAsync();
        }

        TempData["Message"] = $"Notice package sent to {result.Sent} recipient(s)"
            + (result.Failed > 0 ? $"; {result.Failed} failed." : ".");
        if (result.Failed > 0 && result.Errors.Any())
        {
            TempData["Error"] = "Failures: " + string.Join(" | ", result.Errors.Take(5));
        }
        return RedirectToPage("Index");
    }

    // ---- Helpers ----

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

        TemplateOptions = new SelectList(
            NoticeEmailTemplateCatalog.Kinds.Select(k => new { Value = k.Kind, Text = k.Label }),
            "Value", "Text");

        return true;
    }

    private void SeedFromTemplate()
    {
        if (Notice is null) return;
        var tpl = NoticeEmailTemplateCatalog.Build(
            Input.Template,
            Notice.Lease!,
            Notice.SourceTitle ?? Notice.Description ?? Notice.FileName);
        Input.Subject = tpl.Subject;
        Input.Body = tpl.Body;
    }
}
