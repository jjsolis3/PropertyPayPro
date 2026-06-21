using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly PdfService _pdf;
    public DetailsModel(ApplicationDbContext db, PdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public RentalCharge? Charge { get; private set; }
    public List<GeneratedDocument> Documents { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Charge = await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (Charge is null) return NotFound();

        Documents = await _db.GeneratedDocuments
            .Where(d => d.RentalChargeId == id)
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(int id)
    {
        try
        {
            await _pdf.GenerateBillAsync(id);
            TempData["PdfStatus"] = "Bill PDF generated.";
        }
        catch (Exception ex)
        {
            TempData["PdfStatus"] = $"PDF generation failed: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }
}
