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
/// Broadcast a single message to a group of tenants — "water shutoff
/// Tuesday", "office closed", etc. Recipient scope: all tenants with an
/// email on file, active leases only, or tenants at a specific
/// property. Each send is tracked in EmailLog so we have a permanent
/// record of who got what.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole)]
public class BroadcastModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;

    public BroadcastModel(ApplicationDbContext db, MailService mail)
    {
        _db = db;
        _mail = mail;
    }

    public enum RecipientScope
    {
        AllTenants,
        ActiveLeasesOnly,
        SpecificProperty
    }

    public class InputModel
    {
        [Required, StringLength(200), Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required, StringLength(8000), Display(Name = "Message")]
        public string Body { get; set; } = string.Empty;

        [Required, Display(Name = "Send to")]
        public RecipientScope Scope { get; set; } = RecipientScope.ActiveLeasesOnly;

        [Display(Name = "Property")]
        public int? PropertyId { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public SelectList PropertyOptions { get; private set; } = default!;
    public bool MailConfigured => _mail.IsConfigured;

    // Recipient counts by scope, precomputed for the form so the admin
    // sees who they're about to hit before they hit Send.
    public int CountAll { get; private set; }
    public int CountActive { get; private set; }
    public Dictionary<int, int> CountPerProperty { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadRecipientsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRecipientsAsync();
        if (!_mail.IsConfigured)
        {
            ModelState.AddModelError(string.Empty, "SMTP is not configured — cannot send.");
            return Page();
        }
        if (!ModelState.IsValid) return Page();

        if (Input.Scope == RecipientScope.SpecificProperty && !Input.PropertyId.HasValue)
        {
            ModelState.AddModelError(nameof(Input.PropertyId), "Pick a property.");
            return Page();
        }

        var recipients = await ResolveRecipientsAsync(Input.Scope, Input.PropertyId);
        if (recipients.Count == 0)
        {
            TempData["Error"] = "No matching recipients had an email address on file.";
            return RedirectToPage();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _mail.SendBroadcastAsync(baseUrl, recipients, Input.Subject, Input.Body);

        TempData["Message"] = $"Broadcast \"{Input.Subject}\" sent to {result.Sent} recipient(s)"
            + (result.Failed > 0 ? $"; {result.Failed} failed." : ".");
        if (result.Failed > 0 && result.Errors.Any())
        {
            TempData["Error"] = "Failures: " + string.Join(" | ", result.Errors.Take(5));
        }
        return RedirectToPage();
    }

    private async Task LoadRecipientsAsync()
    {
        var properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();
        PropertyOptions = new SelectList(properties, "Id", "Name");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leases = await _db.Leases
            .Include(l => l.Tenants)
            .ToListAsync();

        var allEmails = leases
            .SelectMany(l => l.Tenants)
            .Where(t => !string.IsNullOrWhiteSpace(t.Email))
            .Select(t => t.Email!.Trim().ToLowerInvariant())
            .Distinct()
            .Count();
        CountAll = allEmails;

        var activeLeases = leases
            .Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ToList();
        CountActive = activeLeases
            .SelectMany(l => l.Tenants)
            .Where(t => !string.IsNullOrWhiteSpace(t.Email))
            .Select(t => t.Email!.Trim().ToLowerInvariant())
            .Distinct()
            .Count();

        foreach (var p in properties)
        {
            var count = activeLeases
                .Where(l => l.PropertyId == p.Id)
                .SelectMany(l => l.Tenants)
                .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                .Select(t => t.Email!.Trim().ToLowerInvariant())
                .Distinct()
                .Count();
            CountPerProperty[p.Id] = count;
        }
    }

    private async Task<List<string>> ResolveRecipientsAsync(RecipientScope scope, int? propertyId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = _db.Leases.Include(l => l.Tenants).AsQueryable();

        if (scope == RecipientScope.ActiveLeasesOnly || scope == RecipientScope.SpecificProperty)
        {
            q = q.Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today));
        }
        if (scope == RecipientScope.SpecificProperty && propertyId.HasValue)
        {
            q = q.Where(l => l.PropertyId == propertyId.Value);
        }

        var leases = await q.ToListAsync();
        return leases
            .SelectMany(l => l.Tenants)
            .Where(t => !string.IsNullOrWhiteSpace(t.Email))
            .Select(t => t.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
