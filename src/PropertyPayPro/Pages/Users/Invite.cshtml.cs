using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Users;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class InviteModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;

    public InviteModel(UserManager<ApplicationUser> users, ApplicationDbContext db, MailService mail)
    {
        _users = users;
        _db = db;
        _mail = mail;
    }

    public bool MailConfigured => _mail.IsConfigured;
    public bool EmailSent { get; private set; }
    public string? EmailError { get; private set; }

    public enum InviteRole
    {
        Standard,
        Admin,
        Tenant
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? InviteLink { get; private set; }
    public string? InviteEmail { get; private set; }

    public SelectList TenantOptions { get; private set; } = default!;

    public class InputModel
    {
        [Required, EmailAddress, StringLength(160)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(80), Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(80), Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Invite as")]
        public InviteRole Role { get; set; } = InviteRole.Standard;

        [Display(Name = "Tenant record")]
        public int? TenantId { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadTenantsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTenantsAsync();

        if (Input.Role == InviteRole.Tenant && !Input.TenantId.HasValue)
        {
            ModelState.AddModelError(nameof(Input.TenantId),
                "Pick which Tenant record this login represents.");
        }

        if (!ModelState.IsValid) return Page();

        if (await _users.FindByEmailAsync(Input.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, "A user with that email already exists.");
            return Page();
        }

        if (Input.Role == InviteRole.Tenant && Input.TenantId.HasValue)
        {
            var alreadyLinked = await _db.Users
                .AnyAsync(u => u.TenantId == Input.TenantId.Value);
            if (alreadyLinked)
            {
                ModelState.AddModelError(nameof(Input.TenantId),
                    "That tenant already has a portal login.");
                return Page();
            }
        }

        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            EmailConfirmed = true,
            TenantId = Input.Role == InviteRole.Tenant ? Input.TenantId : null
        };

        // Random initial password so the user must use the reset link to set their own.
        var randomPwd = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var create = await _users.CreateAsync(user, randomPwd);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        if (Input.Role == InviteRole.Admin)
        {
            await _users.AddToRoleAsync(user, IdentitySeed.AdminRole);
        }
        else if (Input.Role == InviteRole.Tenant)
        {
            await _users.AddToRoleAsync(user, IdentitySeed.TenantRole);
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        // The default Identity UI ResetPassword page decodes `code` via
        // WebEncoders.Base64UrlDecode, so we must URL-safe-base64 the raw
        // token before putting it in the URL. Without this, the page sees
        // "+" and "/" characters and fails with "Invalid token".
        var codeParam = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        InviteLink = Url.Page("/Account/ResetPassword", pageHandler: null,
            values: new { area = "Identity", code = codeParam, email = user.Email },
            protocol: Request.Scheme);
        InviteEmail = user.Email;

        // Auto-send the invite via SMTP if it's configured. The link on the
        // page stays as a manual fallback if SMTP fails or isn't set up.
        if (_mail.IsConfigured && !string.IsNullOrEmpty(InviteLink))
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var log = await _mail.SendInviteAsync(
                    baseUrl, user.Email!, user.DisplayName, InviteLink,
                    isTenant: Input.Role == InviteRole.Tenant);
                if (log.Status == EmailStatus.Sent) EmailSent = true;
                else EmailError = log.Error;
            }
            catch (Exception ex)
            {
                EmailError = ex.Message;
            }
        }

        return Page();
    }

    private async Task LoadTenantsAsync()
    {
        // Only tenants that don't already have a portal login are eligible.
        var linkedTenantIds = await _db.Users
            .Where(u => u.TenantId.HasValue)
            .Select(u => u.TenantId!.Value)
            .ToListAsync();

        var tenants = await _db.Tenants
            .Where(t => !linkedTenantIds.Contains(t.Id))
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Select(t => new { t.Id, Label = t.FirstName + " " + t.LastName + (t.Email == null ? "" : " (" + t.Email + ")") })
            .ToListAsync();

        TenantOptions = new SelectList(tenants, "Id", "Label");
    }
}
