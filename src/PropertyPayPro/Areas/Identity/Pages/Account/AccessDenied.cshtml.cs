using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class AccessDeniedModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccessDeniedModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public string CurrentEmail { get; private set; } = "";
    public bool HasNoRole { get; private set; }
    public bool IsTenantWithoutPortal { get; private set; }

    public async Task OnGetAsync()
    {
        if (!_signInManager.IsSignedIn(User)) return;
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return;

        CurrentEmail = user.Email ?? user.UserName ?? "you";
        var roles = await _userManager.GetRolesAsync(user);
        HasNoRole = roles.Count == 0;
        IsTenantWithoutPortal = roles.Contains(IdentitySeed.TenantRole)
            && !roles.Contains(IdentitySeed.AdminRole);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
