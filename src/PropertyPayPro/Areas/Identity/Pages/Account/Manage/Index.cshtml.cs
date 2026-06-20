using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IDocumentStorage _storage;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IDocumentStorage storage)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _storage = storage;
    }

    public string Email { get; private set; } = string.Empty;
    public string? AvatarStorageKey { get; private set; }
    public string CurrentDisplayName { get; private set; } = "User";

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, StringLength(80)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(80)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Phone, StringLength(40)]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        user.FirstName = Input.FirstName;
        user.LastName = Input.LastName;
        if (user.PhoneNumber != Input.PhoneNumber)
        {
            await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
        }

        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = "Your profile has been updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile? avatar)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (avatar is null || avatar.Length == 0)
        {
            StatusMessage = "Choose an image to upload.";
            return RedirectToPage();
        }

        if (avatar.Length > 5 * 1024 * 1024)
        {
            StatusMessage = "Avatar too large (5 MB max).";
            return RedirectToPage();
        }

        if (!avatar.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Only image files are allowed.";
            return RedirectToPage();
        }

        if (!string.IsNullOrWhiteSpace(user.AvatarStorageKey))
        {
            try { await _storage.DeleteAsync(user.AvatarStorageKey); } catch { /* ignore */ }
        }

        await using var stream = avatar.OpenReadStream();
        var key = await _storage.SaveAsync($"avatars/{user.Id}", avatar.FileName, stream);
        user.AvatarStorageKey = key;
        await _userManager.UpdateAsync(user);

        StatusMessage = "Avatar updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAvatarAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(user.AvatarStorageKey))
        {
            try { await _storage.DeleteAsync(user.AvatarStorageKey); } catch { /* ignore */ }
            user.AvatarStorageKey = null;
            await _userManager.UpdateAsync(user);
        }

        StatusMessage = "Avatar removed.";
        return RedirectToPage();
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Email = await _userManager.GetEmailAsync(user) ?? string.Empty;
        AvatarStorageKey = user.AvatarStorageKey;
        CurrentDisplayName = user.DisplayName;
        Input = new InputModel
        {
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user)
        };
    }
}
