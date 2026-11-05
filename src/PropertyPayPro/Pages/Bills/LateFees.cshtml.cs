using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
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
        Message = TempData["Message"] as string;
        Candidates = await _billing.GetLateFeeCandidatesAsync();
    }

    public async Task<IActionResult> OnPostApplyAsync(int chargeId)
    {
        // Managers can view this page but only Admins may change data.
        if (!User.IsInRole(IdentitySeed.AdminRole)) return Forbid();
        var lateFee = await _billing.ApplyLateFeeAsync(chargeId);
        TempData["Message"] = lateFee is null
            ? "No late fee applied: the bill already has one, is paid, or its lease has no late fee set."
            : $"Late fee of {lateFee.AmountDue:C} applied.";
        // Redirect so a refresh doesn't re-submit the form.
        return RedirectToPage();
    }
}
