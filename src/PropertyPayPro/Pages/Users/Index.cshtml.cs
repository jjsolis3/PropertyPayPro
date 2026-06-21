using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Users;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    public IndexModel(UserManager<ApplicationUser> users) => _users = users;

    public List<UserRow> Users { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var all = _users.Users.OrderBy(u => u.Email).ToList();
        foreach (var u in all)
        {
            var roles = await _users.GetRolesAsync(u);
            Users.Add(new UserRow
            {
                Id = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                IsAdmin = roles.Contains(IdentitySeed.AdminRole),
                LockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow
            });
        }
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        var u = await _users.FindByIdAsync(id);
        if (u is null) return NotFound();
        var token = await _users.GeneratePasswordResetTokenAsync(u);
        var link = Url.Page("/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", code = token, email = u.Email },
            protocol: Request.Scheme);
        TempData["ResetLink"] = link;
        TempData["ResetForEmail"] = u.Email;
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
        public bool LockedOut { get; set; }
    }
}
