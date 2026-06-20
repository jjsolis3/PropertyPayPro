using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class GenerateModel : PageModel
{
    private readonly BillingService _billing;
    public GenerateModel(BillingService billing) => _billing = billing;

    [BindProperty]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [BindProperty]
    public int Month { get; set; } = DateTime.UtcNow.Month;

    public string? Message { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Month < 1 || Month > 12)
        {
            ModelState.AddModelError(nameof(Month), "Month must be 1–12.");
            return Page();
        }

        var created = await _billing.GenerateChargesForPeriodAsync(Year, Month);
        Message = $"Generated {created} new bill(s) for {new DateOnly(Year, Month, 1):MMMM yyyy}.";
        return Page();
    }
}
