using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Documents;

[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly PdfService _pdf;

    public IndexModel(ApplicationDbContext db, PdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    [BindProperty(SupportsGet = true)]
    public GeneratedDocumentKind? KindFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? LeaseFilter { get; set; }

    public List<GeneratedDocument> Documents { get; private set; } = new();
    public List<Lease> LeaseOptions { get; private set; } = new();

    public async Task OnGetAsync()
    {
        LeaseOptions = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .OrderBy(l => l.Property!.Name)
            .ToListAsync();

        var q = _db.GeneratedDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .Include(d => d.RentalCharge)
            .Include(d => d.RentPayment)
            .AsQueryable();

        if (KindFilter.HasValue) q = q.Where(d => d.Kind == KindFilter.Value);
        if (LeaseFilter.HasValue) q = q.Where(d => d.LeaseId == LeaseFilter.Value);

        Documents = await q.OrderByDescending(d => d.CreatedUtc).Take(500).ToListAsync();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        try
        {
            var (stream, fileName) = await _pdf.OpenAsync(id);
            return File(stream, "application/pdf", fileName);
        }
        catch
        {
            return NotFound();
        }
    }
}
