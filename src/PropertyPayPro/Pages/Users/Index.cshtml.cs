using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Users;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public IndexModel(UserManager<ApplicationUser> users, ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    public List<UserRow> Users { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var all = _users.Users.Include(u => u.Tenant).OrderBy(u => u.Email).ToList();
        foreach (var u in all)
        {
            var roles = await _users.GetRolesAsync(u);
            Users.Add(new UserRow
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                IsAdmin = roles.Contains(IdentitySeed.AdminRole),
                IsManager = roles.Contains(IdentitySeed.ManagerRole),
                IsTenant = roles.Contains(IdentitySeed.TenantRole),
                TenantId = u.TenantId,
                TenantName = u.Tenant is null ? null : $"{u.Tenant.FirstName} {u.Tenant.LastName}".Trim(),
                LockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        var token = await _users.GeneratePasswordResetTokenAsync(u);
        // Base64-URL-encode the raw token so the default Identity UI page
        // (which calls WebEncoders.Base64UrlDecode) can decode it back.
        var codeParam = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Page("/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", code = codeParam, email = u.Email },
            protocol: Request.Scheme);
        TempData["ResetLink"] = link;
        TempData["ResetForEmail"] = u.Email;
        return RedirectToPage();
    }

    /// <summary>
    /// Admin override: set a user's password directly, without going through
    /// the email-reset-link flow. Used when the reset-link flow fails or when
    /// the admin needs to give the user a specific known password.
    /// </summary>
    public async Task<IActionResult> OnPostSetPasswordAsync(string id, string newPassword)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["Error"] = "New password must be at least 8 characters.";
            return RedirectToPage();
        }

        // Skip the reset-token roundtrip entirely — we're admin, we're
        // authenticated, we're already on a page that requires the Admin
        // role. Generate a token internally + apply it in one step so the
        // password rules from Identity's options still get enforced.
        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var result = await _users.ResetPasswordAsync(u, token, newPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = $"Set password failed for {u.Email}: "
                + string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToPage();
        }

        TempData["Message"] = $"Password updated for {u.Email}. Share it with them securely.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleLockoutAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        var current = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;
        await _users.SetLockoutEndDateAsync(u, current ? null : DateTimeOffset.UtcNow.AddYears(100));
        TempData["Message"] = current ? $"{u.Email} unlocked." : $"{u.Email} locked.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAdminAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        if (await _users.IsInRoleAsync(u, IdentitySeed.AdminRole))
        {
            // Refuse to remove the last admin.
            var admins = await _users.GetUsersInRoleAsync(IdentitySeed.AdminRole);
            if (admins.Count <= 1)
            {
                TempData["Error"] = "Cannot remove the only remaining administrator.";
                return RedirectToPage();
            }
            await _users.RemoveFromRoleAsync(u, IdentitySeed.AdminRole);
        }
        else
        {
            await _users.AddToRoleAsync(u, IdentitySeed.AdminRole);
        }
        return RedirectToPage();
    }

    public class UserRow
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool IsTenant { get; set; }
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
        public bool LockedOut { get; set; }
    }
}
