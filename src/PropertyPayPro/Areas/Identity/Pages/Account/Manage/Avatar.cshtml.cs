using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class AvatarModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDocumentStorage _storage;

    public AvatarModel(UserManager<ApplicationUser> userManager, IDocumentStorage storage)
    {
        _userManager = userManager;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || string.IsNullOrWhiteSpace(user.AvatarStorageKey))
        {
            return NotFound();
        }

        try
        {
            var stream = await _storage.OpenReadAsync(user.AvatarStorageKey);
            var ext = Path.GetExtension(user.AvatarStorageKey).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            Response.Headers.CacheControl = "private, max-age=60";
            return File(stream, contentType);
        }
        catch
        {
            return NotFound();
        }
    }
}
