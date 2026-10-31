using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.Pdfs;

namespace PropertyPayPro.Pages.Notices;

/// <summary>
/// Two-step notice authoring:
///   1. Pick lease + template kind (GET populates defaults into the form).
///   2. Review/edit the pre-filled title and body, then Generate.
/// Generating builds the PDF, saves it via IDocumentStorage, and attaches
/// it to the lease as a LeaseDocument{Type=Notice}. If the "Email to
/// tenant" box is checked, the PDF is emailed as an attachment to every
/// tenant on the lease with an email address; each send is tracked in
/// EmailLog.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly MailService _mail;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditLog _audit;
    private readonly NoticeTemplateService _tpl;

    public CreateModel(
        ApplicationDbContext db,
        IDocumentStorage storage,
        MailService mail,
        IWebHostEnvironment env,
        IAuditLog audit,
        NoticeTemplateService tpl)
    {
        _db = db;
        _storage = storage;
        _mail = mail;
        _env = env;
        _audit = audit;
        _tpl = tpl;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public SelectList LeaseOptions { get; private set; } = default!;
    public SelectList TemplateOptions { get; private set; } = default!;
    public bool MailConfigured => _mail.IsConfigured;
    public Lease? SelectedLease { get; private set; }
    public int RecipientCount { get; private set; }

    public class InputModel
    {
        [Required, Display(Name = "Lease")]
        public int? LeaseId { get; set; }

        [Display(Name = "Template")]
        public int? TemplateId { get; set; }

        [Required, StringLength(120), Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(8000), Display(Name = "Body")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Email PDF to tenant(s) with email on file")]
        public bool EmailToTenant { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int? leaseId, int? templateId)
    {
        await LoadDropdownsAsync();

        if (leaseId.HasValue)
        {
            Input.LeaseId = leaseId;
            Input.TemplateId = templateId;
            await SeedFromTemplateAsync();
        }
        return Page();
    }

    /// <summary>
    /// Reload the pre-filled defaults after the user changed lease or
    /// template. Wired to a "Load template" button on the form.
    /// </summary>
    public async Task<IActionResult> OnPostReloadAsync()
    {
        await LoadDropdownsAsync();
        ModelState.Clear();
        await SeedFromTemplateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        await LoadDropdownsAsync();
        if (!ModelState.IsValid) return Page();

        var lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == Input.LeaseId);
        if (lease is null)
        {
            ModelState.AddModelError(string.Empty, "That lease no longer exists.");
            return Page();
        }

        // Build the PDF.
        var logo = TryLoadLogo();
        var pdfBytes = NoticePdfBuilder.Build(Input.Title, Input.Body, lease, logo);

        // Store it under notices/{leaseId}/ and attach as a LeaseDocument.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var safeTitle = string.Join('-',
            Input.Title.Trim().Split(Path.GetInvalidFileNameChars()));
        if (safeTitle.Length > 60) safeTitle = safeTitle[..60];
        var fileName = $"{today:yyyyMMdd}_{safeTitle}.pdf";

        using var ms = new MemoryStream(pdfBytes);
        var storageKey = await _storage.SaveAsync($"notices/{lease.Id}", fileName, ms);

        var doc = new LeaseDocument
        {
            LeaseId = lease.Id,
            Type = LeaseDocumentType.Notice,
            FileName = fileName,
            ContentType = "application/pdf",
            SizeBytes = pdfBytes.Length,
            StorageKey = storageKey,
            Description = Input.Title,
            EffectiveDate = today,
            // Persist the source title + body so the notice can be
            // reopened, edited, and the PDF regenerated from scratch.
            SourceTitle = Input.Title,
            SourceBody = Input.Body
        };
        _db.LeaseDocuments.Add(doc);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("NoticeCreated",
            entityType: nameof(LeaseDocument),
            entityId: doc.Id,
            details: $"Lease #{lease.Id}; title=\"{Input.Title}\"; file={fileName}");

        // Optionally email each tenant with an email on file.
        var emailSummary = "";
        if (Input.EmailToTenant && _mail.IsConfigured)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var recipients = lease.Tenants
                .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                .Select(t => t.Email!)
                .ToList();

            var sent = 0;
            var failed = 0;
            foreach (var addr in recipients)
            {
                var log = await _mail.SendNoticeAsync(baseUrl, addr, lease.Id, Input.Title, fileName, pdfBytes);
                if (log.Status == EmailStatus.Sent) sent++; else failed++;
            }
            if (sent > 0)
            {
                doc.SentCount += sent;
                doc.LastSentUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await _audit.WriteAsync("NoticeSent",
                    entityType: nameof(LeaseDocument),
                    entityId: doc.Id,
                    details: $"Delivered at generation: {sent}/{recipients.Count}");
            }
            emailSummary = recipients.Count == 0
                ? " No tenant on this lease has an email on file — skipped emailing."
                : $" Emailed to {sent} recipient(s){(failed > 0 ? $"; {failed} failed" : "")}.";
        }

        TempData["Message"] = $"Notice \"{Input.Title}\" saved to the lease.{emailSummary}";
        return RedirectToPage("Index");
    }

    private async Task LoadDropdownsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .OrderByDescending(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ThenBy(l => l.Property!.Name)
            .Select(l => new
            {
                l.Id,
                Label = l.Property!.Name + " — " +
                    string.Join(", ", l.Tenants.Select(t => t.FirstName + " " + t.LastName))
            })
            .ToListAsync();
        LeaseOptions = new SelectList(leases, "Id", "Label");

        var templates = await _tpl.ListAsync(NoticeTemplateScope.PdfBody);
        TemplateOptions = new SelectList(
            templates.Select(t => new { Value = t.Id, Text = t.Label }),
            "Value", "Text");

        if (Input.LeaseId.HasValue)
        {
            SelectedLease = await _db.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenants)
                .FirstOrDefaultAsync(l => l.Id == Input.LeaseId.Value);
            RecipientCount = SelectedLease?.Tenants
                .Count(t => !string.IsNullOrWhiteSpace(t.Email)) ?? 0;
        }
    }

    private async Task SeedFromTemplateAsync()
    {
        if (!Input.LeaseId.HasValue || !Input.TemplateId.HasValue) return;
        var lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == Input.LeaseId.Value);
        if (lease is null) return;

        var template = await _tpl.GetAsync(Input.TemplateId.Value);
        if (template is null) return;

        var outstanding = lease.Charges.Sum(c => c.Balance);
        var rendered = _tpl.Render(template, new TemplateContext(
            Lease: lease, OutstandingAmount: outstanding));
        Input.Title = rendered.TitleOrSubject;
        Input.Body = rendered.Body;
    }

    private byte[]? TryLoadLogo()
    {
        var path = Path.Combine(_env.WebRootPath ?? "wwwroot", "img", "brand", "PPS_Logo_Main.png");
        return System.IO.File.Exists(path)
            ? System.IO.File.ReadAllBytes(path)
            : null;
    }
}
