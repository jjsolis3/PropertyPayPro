using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.Pdfs;

namespace PropertyPayPro.Pages.Notices;

/// <summary>
/// Reopens an already-generated notice to tweak the title/body and
/// regenerate the PDF. The LeaseDocument row keeps the same Id, so any
/// link (Lease Hub, dashboard widget, already-sent email with a
/// "Preview" URL in it) stays valid; only the stored file bytes and the
/// FileName/SizeBytes/UploadedOn metadata change.
///
/// Admin-only. If the notice was already delivered to a tenant, the
/// delivered PDF is of course unchanged — we overwrite the server
/// copy, not the recipient's attachment.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IWebHostEnvironment _env;

    public EditModel(
        ApplicationDbContext db,
        IDocumentStorage storage,
        IWebHostEnvironment env)
    {
        _db = db;
        _storage = storage;
        _env = env;
    }

    public class InputModel
    {
        public int Id { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "";
        [Required, StringLength(16000)] public string Body { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public LeaseDocument? Notice { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var doc = await Load(id);
        if (doc is null) return NotFound();

        Notice = doc;
        Input.Id = doc.Id;
        Input.Title = doc.SourceTitle ?? doc.Description ?? "";
        Input.Body = doc.SourceBody ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var doc = await Load(Input.Id);
        if (doc is null) return NotFound();
        Notice = doc;

        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrEmpty(doc.SourceBody) && string.IsNullOrEmpty(doc.SourceTitle))
        {
            // Pre-schema upload: no source text available. We still let
            // the admin edit the title/body, but flag it in the TempData
            // message so there's no mystery.
            TempData["Info"] = "This notice predates editable source text — PDF rebuilt from the new input.";
        }

        // Regenerate the PDF with the new title + body.
        var logo = TryLoadLogo();
        var pdfBytes = NoticePdfBuilder.Build(Input.Title, Input.Body, doc.Lease!, logo);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var safeTitle = string.Join('-',
            Input.Title.Trim().Split(Path.GetInvalidFileNameChars()));
        if (safeTitle.Length > 60) safeTitle = safeTitle[..60];
        var newFileName = $"{today:yyyyMMdd}_{safeTitle}.pdf";

        // Overwrite the stored file (new path), then best-effort remove
        // the old one if the key changed.
        var oldKey = doc.StorageKey;
        using (var ms = new MemoryStream(pdfBytes))
        {
            doc.StorageKey = await _storage.SaveAsync($"notices/{doc.LeaseId}", newFileName, ms);
        }
        if (!string.Equals(oldKey, doc.StorageKey, StringComparison.Ordinal))
        {
            try { await _storage.DeleteAsync(oldKey); } catch { /* ignore missing */ }
        }

        doc.FileName = newFileName;
        doc.SizeBytes = pdfBytes.Length;
        doc.UploadedOn = DateTime.UtcNow;
        doc.Description = Input.Title;
        doc.SourceTitle = Input.Title;
        doc.SourceBody = Input.Body;
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Notice \"{Input.Title}\" regenerated.";
        return RedirectToPage("Index");
    }

    private async Task<LeaseDocument?> Load(int id)
    {
        return await _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == LeaseDocumentType.Notice);
    }

    private byte[]? TryLoadLogo()
    {
        var path = Path.Combine(_env.WebRootPath ?? "wwwroot", "img", "brand", "PPS_Logo_Main.png");
        return System.IO.File.Exists(path)
            ? System.IO.File.ReadAllBytes(path)
            : null;
    }
}
