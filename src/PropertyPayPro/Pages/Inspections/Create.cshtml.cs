using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Inspections;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    public class InputModel
    {
        [Required] public int? LeaseId { get; set; }
        [Required] public InspectionKind Kind { get; set; } = InspectionKind.MoveIn;
        [Required, DataType(DataType.Date)]
        public DateOnly ScheduledFor { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [StringLength(120)] public string? ConductedBy { get; set; }
        public bool TenantPresent { get; set; } = true;
        [Display(Name = "Seed with standard room / item checklist")]
        public bool SeedFromTemplate { get; set; } = true;
        [Display(Name = "Auto-link to most recent completed move-in (move-out only)")]
        public bool AutoLinkMoveIn { get; set; } = true;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public SelectList LeaseOptions { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? leaseId)
    {
        if (leaseId.HasValue) Input.LeaseId = leaseId;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();

        var lease = await _db.Leases.FindAsync(Input.LeaseId);
        if (lease is null)
        {
            ModelState.AddModelError(nameof(Input.LeaseId), "That lease no longer exists.");
            return Page();
        }

        var inspection = new Inspection
        {
            LeaseId = lease.Id,
            Kind = Input.Kind,
            ScheduledFor = Input.ScheduledFor,
            ConductedBy = Input.ConductedBy,
            TenantPresent = Input.TenantPresent,
            Status = InspectionStatus.Draft
        };

        if (Input.Kind == InspectionKind.MoveOut && Input.AutoLinkMoveIn)
        {
            var priorMoveIn = await _db.Inspections
                .Where(i => i.LeaseId == lease.Id
                    && i.Kind == InspectionKind.MoveIn
                    && i.Status == InspectionStatus.Completed)
                .OrderByDescending(i => i.CompletedOn ?? i.ScheduledFor)
                .FirstOrDefaultAsync();
            if (priorMoveIn is not null)
            {
                inspection.PairedMoveInId = priorMoveIn.Id;
            }
        }

        if (Input.SeedFromTemplate)
        {
            foreach (var seed in InspectionChecklistTemplate.Seed())
            {
                inspection.Items.Add(seed);
            }
        }

        _db.Inspections.Add(inspection);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Inspection created — {inspection.Items.Count} item(s) seeded.";
        return RedirectToPage("Details", new { id = inspection.Id });
    }

    private async Task LoadAsync()
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
    }
}
