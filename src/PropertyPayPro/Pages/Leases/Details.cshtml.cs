using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;

    public DetailsModel(ApplicationDbContext db, IDocumentStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public Lease? Lease { get; private set; }
    public List<RentalCharge> Charges { get; private set; } = new();
    public List<GeneratedDocument> GeneratedDocs { get; private set; } = new();

    public decimal TotalBilled { get; private set; }
    public decimal TotalPaid { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public int UnpaidCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadAsync(id);
        return Lease is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(int leaseId, IFormFile? file, string? description)
    {
        var lease = await _db.Leases.FindAsync(leaseId);
        if (lease is null) return NotFound();

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose a file to upload.");
            await LoadAsync(leaseId);
            return Page();
        }

        if (file.Length > 25 * 1024 * 1024)
        {
            ModelState.AddModelError(string.Empty, "File too large (25 MB max).");
            await LoadAsync(leaseId);
            return Page();
        }

        await using var stream = file.OpenReadStream();
        var key = await _storage.SaveAsync($"leases/{leaseId}", file.FileName, stream);

        _db.LeaseDocuments.Add(new LeaseDocument
        {
            LeaseId = leaseId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            Description = description
        });
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id = leaseId });
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id, int documentId)
    {
        var doc = await _db.LeaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.LeaseId == id);
        if (doc is null) return NotFound();

        var stream = await _storage.OpenReadAsync(doc.StorageKey);
        return File(stream, doc.ContentType, doc.FileName);
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(int id, int documentId)
    {
        var doc = await _db.LeaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.LeaseId == id);
        if (doc is null) return RedirectToPage(new { id });

        try { await _storage.DeleteAsync(doc.StorageKey); } catch { /* ignore missing file */ }
        _db.LeaseDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(int id)
    {
        Lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Payments).ThenInclude(p => p.Allocations)
            .Include(l => l.Documents)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (Lease is null) return;

        Charges = Lease.Charges.OrderByDescending(c => c.BillingPeriodStart).ToList();
        TotalBilled = Charges.Sum(c => c.AmountDue);
        TotalPaid = Charges.Sum(c => c.AmountPaid);
        CurrentBalance = Charges.Sum(c => c.Balance);
        UnpaidCount = Charges.Count(c => c.Balance > 0);

        GeneratedDocs = await _db.GeneratedDocuments
            .Where(d => d.LeaseId == id)
            .OrderByDescending(d => d.CreatedUtc)
            .Take(20)
            .ToListAsync();
    }
}
