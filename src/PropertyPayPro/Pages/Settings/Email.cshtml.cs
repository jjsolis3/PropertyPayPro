using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EmailModel : PageModel
{
    private readonly AppSettingsService _settings;
    private readonly IEmailSender _sender;
    private readonly UserManager<ApplicationUser> _users;
    private readonly EmailOptions _options;

    public EmailModel(
        AppSettingsService settings,
        IEmailSender sender,
        UserManager<ApplicationUser> users,
        IOptions<EmailOptions> options)
    {
        _settings = settings;
        _sender = sender;
        _users = users;
        _options = options.Value;
    }

    public AppSettings Current { get; private set; } = new();
    public EmailOptions Options => _options;

    [BindProperty]
    public string? FromEmailOverride { get; set; }

    [BindProperty]
    public string? FromNameOverride { get; set; }

    [BindProperty]
    public string? TestRecipient { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Current = await _settings.GetAsync();
        FromEmailOverride = Current.FromEmailOverride;
        FromNameOverride = Current.FromNameOverride;
        TestRecipient = (await _users.GetUserAsync(User))?.Email;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        Current = await _settings.GetAsync();
        Current.FromEmailOverride = string.IsNullOrWhiteSpace(FromEmailOverride) ? null : FromEmailOverride.Trim();
        Current.FromNameOverride = string.IsNullOrWhiteSpace(FromNameOverride) ? null : FromNameOverride.Trim();
        await _settings.SaveAsync(Current);
        TempData["Message"] = "Email overrides saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestAsync()
    {
        if (string.IsNullOrWhiteSpace(TestRecipient))
        {
            TempData["Error"] = "Enter a recipient email.";
            return RedirectToPage();
        }
        if (!_sender.IsConfigured)
        {
            TempData["Error"] = "SMTP is not configured. Set SMTP_HOST / SMTP_USER / SMTP_PASSWORD env vars.";
            return RedirectToPage();
        }

        try
        {
            var body = $@"<html><body style=""font-family:sans-serif;padding:24px;"">
                <h2 style=""color:#1f3a8a;"">PropertyPayPro — SMTP test</h2>
                <p>If you're reading this, SMTP is working correctly.</p>
                <p style=""color:#666;"">Sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</p>
                </body></html>";
            await _sender.SendAsync(TestRecipient, "PropertyPayPro test email", body);
            TempData["Message"] = $"Test email sent to {TestRecipient}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Send failed: {ex.Message}";
        }
        return RedirectToPage();
    }
}
