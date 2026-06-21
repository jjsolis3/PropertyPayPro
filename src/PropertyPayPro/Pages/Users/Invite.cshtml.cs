using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Users;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class InviteModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    public InviteModel(UserManager<ApplicationUser> users) => _users = users;

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? InviteLink { get; private set; }
    public string? InviteEmail { get; private set; }

    public class InputModel
    {
        [Required, EmailAddress, StringLength(160)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(80), Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(80), Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Make admin")]
        public bool MakeAdmin { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (await _users.FindByEmailAsync(Input.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, "A user with that email already exists.");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            EmailConfirmed = true
        };

        // Random initial password so the user must use the reset link to set their own.
        var randomPwd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var create = await _users.CreateAsync(user, randomPwd);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        if (Input.MakeAdmin)
        {
            await _users.AddToRoleAsync(user, IdentitySeed.AdminRole);
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        InviteLink = Url.Page("/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", code = token, email = user.Email },
            protocol: Request.Scheme);
        InviteEmail = user.Email;
        return Page();
    }
}
