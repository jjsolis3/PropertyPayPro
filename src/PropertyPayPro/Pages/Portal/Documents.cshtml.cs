using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Portal;

public class DocumentsModel : PortalPageBase
{
    private readonly IDocumentStorage _storage;

    public DocumentsModel(ApplicationDbContext db, UserManager<ApplicationUser> users, IDocumentStorage storage)
        : base(db, users)
    {
        _storage = storage;
    }

    public List<LeaseDocument> Documents { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        var leaseIds = AllLeases.Select(l => l.Id).ToList();
        Documents = await Db.LeaseDocuments
            .Where(d => leaseIds.Contains(d.LeaseId))
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .OrderByDescending(d => d.UploadedOn)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int documentId)
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        var leaseIds = AllLeases.Select(l => l.Id).ToList();
        var doc = await Db.LeaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && leaseIds.Contains(d.LeaseId));
        if (doc is null) return NotFound();

        var stream = await _storage.OpenReadAsync(doc.StorageKey);
        return File(stream, doc.ContentType, doc.FileName);
    }
}
