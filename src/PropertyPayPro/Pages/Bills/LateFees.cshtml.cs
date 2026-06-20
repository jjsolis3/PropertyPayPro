using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class LateFeesModel : PageModel
{
    private readonly BillingService _billing;
    public LateFeesModel(BillingService billing) => _billing = billing;

    public List<RentalCharge> Candidates { get; private set; } = new();
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        Candidates = await _billing.GetLateFeeCandidatesAsync();
    }

    public async Task<IActionResult> OnPostApplyAsync(int chargeId)
    {
        var lateFee = await _billing.ApplyLateFeeAsync(chargeId);
        Message = lateFee is null
            ? "Could not apply late fee — the bill may already have one, or the lease has no late fee configured."
            : $"Late fee of {lateFee.AmountDue:C} applied.";
        Candidates = await _billing.GetLateFeeCandidatesAsync();
        return Page();
    }
}
