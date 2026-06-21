using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PropertyPayPro.Areas.Identity.Pages.Account;

// Public self-registration is disabled. Users are created by an admin
// from /Users/Invite. This page is intentionally a 404.
[AllowAnonymous]
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => NotFound();
    public IActionResult OnPost() => NotFound();
}
