using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class BillingModel : PageModel
{
    private readonly AppSettingsService _settings;
    public BillingModel(AppSettingsService settings) => _settings = settings;

    [BindProperty, System.ComponentModel.DataAnnotations.Range(1, 31)]
    public int DefaultRentDueDay { get; set; }

    [BindProperty, System.ComponentModel.DataAnnotations.Range(0, 60)]
    public int DefaultLateFeeGraceDays { get; set; }

    [BindProperty, System.ComponentModel.DataAnnotations.Range(0, 10_000)]
    public decimal DefaultLateFeeAmount { get; set; }

    public AppSettings Current { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Current = await _settings.GetAsync();
        DefaultRentDueDay = Current.DefaultRentDueDay;
        DefaultLateFeeGraceDays = Current.DefaultLateFeeGraceDays;
        DefaultLateFeeAmount = Current.DefaultLateFeeAmount;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Current = await _settings.GetAsync();
            return Page();
        }

        Current = await _settings.GetAsync();
        Current.DefaultRentDueDay = DefaultRentDueDay;
        Current.DefaultLateFeeGraceDays = DefaultLateFeeGraceDays;
        Current.DefaultLateFeeAmount = DefaultLateFeeAmount;
        await _settings.SaveAsync(Current);
        TempData["Message"] = "Billing defaults saved.";
        return RedirectToPage();
    }
}
