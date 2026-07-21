using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.Pdfs;

namespace PropertyPayPro.Pages.Inspections;

/// <summary>
/// Editor for a single inspection. Draft inspections are fully editable
/// (add/edit/delete items, upload/remove photos, edit metadata).
/// Completed inspections are read-only. Completing runs the PDF
/// generator and attaches the report to the lease as a
/// LeaseDocument{Type=MoveInChecklist or MoveOutInspection}.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IWebHostEnvironment _env;

    public DetailsModel(
        ApplicationDbContext db,
        IDocumentStorage storage,
        IWebHostEnvironment env)
    {
        _db = db;
        _storage = storage;
        _env = env;
    }

    public Inspection? Inspection { get; private set; }
    public LeaseDocument? GeneratedDoc { get; private set; }
    public bool IsAdmin => User.IsInRole(IdentitySeed.AdminRole);
    public bool CanEdit => IsAdmin && Inspection?.Status == InspectionStatus.Draft;
    public IEnumerable<IGrouping<string, InspectionItem>> RoomGroups =>
        (Inspection?.Items ?? new()).OrderBy(i => i.Order).GroupBy(i => i.Room);

    public class HeaderInput
    {
        public int Id { get; set; }
        [DataType(DataType.Date)] public DateOnly ScheduledFor { get; set; }
        [StringLength(120)] public string? ConductedBy { get; set; }
        public bool TenantPresent { get; set; }
        [StringLength(2000)] public string? OverallNotes { get; set; }
    }

    public class ItemInput
    {
        public int Id { get; set; }
        public int InspectionId { get; set; }
        [Required, StringLength(80)] public string Room { get; set; } = "";
        [Required, StringLength(120)] public string Item { get; set; } = "";
        public InspectionCondition Condition { get; set; }
        [StringLength(2000)] public string? Notes { get; set; }
        [Range(0, 100_000)] public decimal? DeductionAmount { get; set; }
        [StringLength(500)] public string? DeductionReason { get; set; }
    }

    public class NewItemInput
    {
        public int InspectionId { get; set; }
        [Required, StringLength(80)] public string Room { get; set; } = "";
        [Required, StringLength(120)] public string Item { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadAsync(id);
        return Inspection is null ? NotFound() : Page();
    }

    // ---- Handlers ----

    public async Task<IActionResult> OnPostUpdateHeaderAsync([FromForm] HeaderInput input)
    {
        var i = await _db.Inspections.FirstOrDefaultAsync(x => x.Id == input.Id);
        if (i is null) return NotFound();
        if (!CanMutate(i)) return Forbid();

        i.ScheduledFor = input.ScheduledFor;
        i.ConductedBy = input.ConductedBy;
        i.TenantPresent = input.TenantPresent;
        i.OverallNotes = input.OverallNotes;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = i.Id });
    }

    public async Task<IActionResult> OnPostUpdateItemAsync([FromForm] ItemInput input)
    {
        var item = await _db.InspectionItems
            .Include(x => x.Inspection)
            .FirstOrDefaultAsync(x => x.Id == input.Id);
        if (item is null) return NotFound();
        if (!CanMutate(item.Inspection!)) return Forbid();

        item.Room = input.Room;
        item.Item = input.Item;
        item.Condition = input.Condition;
        item.Notes = input.Notes;
        item.DeductionAmount = input.DeductionAmount;
        item.DeductionReason = input.DeductionReason;
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = input.InspectionId });
    }

    public async Task<IActionResult> OnPostAddItemAsync([FromForm] NewItemInput input)
    {
        var i = await _db.Inspections.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == input.InspectionId);
        if (i is null) return NotFound();
        if (!CanMutate(i)) return Forbid();

        var maxOrder = i.Items.Where(x => x.Room == input.Room).Select(x => (int?)x.Order).Max() ?? 0;
        _db.InspectionItems.Add(new InspectionItem
        {
            InspectionId = i.Id,
            Room = input.Room,
            Item = input.Item,
            Condition = InspectionCondition.NotAssessed,
            Order = maxOrder + 10
        });
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = i.Id });
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(int inspectionId, int itemId)
    {
        var item = await _db.InspectionItems
            .Include(x => x.Photos)
            .Include(x => x.Inspection)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item is null) return RedirectToPage(new { id = inspectionId });
        if (!CanMutate(item.Inspection!)) return Forbid();

        foreach (var photo in item.Photos.ToList())
        {
            try { await _storage.DeleteAsync(photo.StorageKey); } catch { /* ignore */ }
        }
        _db.InspectionItems.Remove(item);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = inspectionId });
    }

    public async Task<IActionResult> OnPostUploadPhotoAsync(int inspectionId, int itemId, IFormFile? file, string? caption)
    {
        var item = await _db.InspectionItems
            .Include(x => x.Inspection)
            .FirstOrDefaultAsync(x => x.Id == itemId);
        if (item is null) return NotFound();
        if (!CanMutate(item.Inspection!)) return Forbid();
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Choose a photo to upload.";
            return RedirectToPage(new { id = inspectionId });
        }
        if (file.Length > 15 * 1024 * 1024)
        {
            TempData["Error"] = "Photo too large (15 MB max).";
            return RedirectToPage(new { id = inspectionId });
        }

        await using var stream = file.OpenReadStream();
        var key = await _storage.SaveAsync($"inspections/{inspectionId}", file.FileName, stream);

        _db.InspectionPhotos.Add(new InspectionPhoto
        {
            InspectionItemId = itemId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            Caption = caption
        });
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = inspectionId });
    }

    public async Task<IActionResult> OnPostDeletePhotoAsync(int inspectionId, int photoId)
    {
        var photo = await _db.InspectionPhotos
            .Include(p => p.InspectionItem).ThenInclude(i => i!.Inspection)
            .FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo is null) return RedirectToPage(new { id = inspectionId });
        if (!CanMutate(photo.InspectionItem!.Inspection!)) return Forbid();

        try { await _storage.DeleteAsync(photo.StorageKey); } catch { /* ignore */ }
        _db.InspectionPhotos.Remove(photo);
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id = inspectionId });
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id)
    {
        var i = await _db.Inspections
            .Include(x => x.Lease).ThenInclude(l => l!.Property)
            .Include(x => x.Lease).ThenInclude(l => l!.Tenants)
            .Include(x => x.Items).ThenInclude(it => it.Photos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        if (!CanMutate(i)) return Forbid();

        // Generate PDF.
        var pdfBytes = InspectionPdfBuilder.Build(i, TryLoadLogo());
        var kindLabel = i.Kind == InspectionKind.MoveIn ? "MoveIn" : "MoveOut";
        var fileName = $"{i.ScheduledFor:yyyyMMdd}_{kindLabel}_Inspection_{i.Lease?.Property?.Name}.pdf";
        // Sanitize filename.
        fileName = string.Join('-', fileName.Split(Path.GetInvalidFileNameChars()));

        using var ms = new MemoryStream(pdfBytes);
        var storageKey = await _storage.SaveAsync($"inspections/{i.Id}", fileName, ms);

        var docType = i.Kind == InspectionKind.MoveIn
            ? LeaseDocumentType.MoveInChecklist
            : LeaseDocumentType.MoveOutInspection;
        var doc = new LeaseDocument
        {
            LeaseId = i.LeaseId,
            Type = docType,
            FileName = fileName,
            ContentType = "application/pdf",
            SizeBytes = pdfBytes.Length,
            StorageKey = storageKey,
            Description = $"{(i.Kind == InspectionKind.MoveIn ? "Move-in" : "Move-out")} inspection #{i.Id}",
            EffectiveDate = i.ScheduledFor
        };
        _db.LeaseDocuments.Add(doc);
        await _db.SaveChangesAsync();

        i.GeneratedDocumentId = doc.Id;
        i.Status = InspectionStatus.Completed;
        i.CompletedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync();

        TempData["Message"] = "Inspection completed. Report attached to the lease.";
        return RedirectToPage(new { id = i.Id });
    }

    public async Task<IActionResult> OnGetPhotoAsync(int id, int photoId)
    {
        var photo = await _db.InspectionPhotos
            .Include(p => p.InspectionItem)
            .FirstOrDefaultAsync(p => p.Id == photoId
                && p.InspectionItem!.InspectionId == id);
        if (photo is null) return NotFound();
        var stream = await _storage.OpenReadAsync(photo.StorageKey);
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{photo.FileName}\"";
        return File(stream, photo.ContentType);
    }

    // ---- Helpers ----

    private bool CanMutate(Inspection i) =>
        User.IsInRole(IdentitySeed.AdminRole) && i.Status == InspectionStatus.Draft;

    private async Task LoadAsync(int id)
    {
        Inspection = await _db.Inspections
            .Include(i => i.Lease).ThenInclude(l => l!.Property)
            .Include(i => i.Lease).ThenInclude(l => l!.Tenants)
            .Include(i => i.Items).ThenInclude(it => it.Photos)
            .Include(i => i.PairedMoveIn)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Inspection?.GeneratedDocumentId.HasValue == true)
        {
            GeneratedDoc = await _db.LeaseDocuments
                .FirstOrDefaultAsync(d => d.Id == Inspection.GeneratedDocumentId.Value);
        }
    }

    private byte[]? TryLoadLogo()
    {
        var path = Path.Combine(_env.WebRootPath ?? "wwwroot", "img", "brand", "PPS_Logo_Main.png");
        return System.IO.File.Exists(path)
            ? System.IO.File.ReadAllBytes(path)
            : null;
    }
}
