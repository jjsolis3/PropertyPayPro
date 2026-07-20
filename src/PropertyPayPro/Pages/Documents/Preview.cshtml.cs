using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Documents;

/// <summary>
/// Inline preview of a LeaseDocument. Serves both roles:
///   • Admin/Manager sees any lease document in the system.
///   • Tenant sees only documents attached to a lease they're on.
/// The raw file bytes are streamed through /Documents/Preview?id=&handler=Stream
/// which the &lt;iframe&gt; / &lt;img&gt; on the page points at. The stream
/// endpoint applies the same scoping as the page itself.
/// </summary>
[Authorize]
public class PreviewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly UserManager<ApplicationUser> _users;

    public PreviewModel(
        ApplicationDbContext db,
        IDocumentStorage storage,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _storage = storage;
        _users = users;
    }

    public LeaseDocument? Document { get; private set; }
    public string? BackUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var doc = await LoadScopedAsync(id);
        if (doc is null) return NotFound();
        Document = doc;

        var user = await _users.GetUserAsync(User);
        var isTenant = user is not null
            && await _users.IsInRoleAsync(user, IdentitySeed.TenantRole)
            && !await _users.IsInRoleAsync(user, IdentitySeed.AdminRole);
        BackUrl = isTenant ? "/Portal/Documents" : $"/Leases/Details/{doc.LeaseId}";

        return Page();
    }

    /// <summary>
    /// Streams the raw bytes with inline Content-Disposition so browsers
    /// display them instead of downloading. Same scoping as the page.
    /// </summary>
    public async Task<IActionResult> OnGetStreamAsync(int id)
    {
        var doc = await LoadScopedAsync(id);
        if (doc is null) return NotFound();

        var stream = await _storage.OpenReadAsync(doc.StorageKey);
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.FileName}\"";
        return File(stream, doc.ContentType);
    }

    /// <summary>
    /// Loads the document if the current user is allowed to see it. Admins
    /// and managers see anything; tenants only see documents on their own
    /// leases.
    /// </summary>
    private async Task<LeaseDocument?> LoadScopedAsync(int id)
    {
        var doc = await _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return null;

        var user = await _users.GetUserAsync(User);
        if (user is null) return null;

        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(IdentitySeed.AdminRole) || roles.Contains(IdentitySeed.ManagerRole))
        {
            return doc;
        }
        if (roles.Contains(IdentitySeed.TenantRole) && user.TenantId.HasValue)
        {
            // Tenant may see documents on any lease they're a party to.
            var onLease = doc.Lease?.Tenants.Any(t => t.Id == user.TenantId.Value) ?? false;
            return onLease ? doc : null;
        }
        return null;
    }
}
