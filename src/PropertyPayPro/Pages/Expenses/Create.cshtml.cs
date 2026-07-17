using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Expenses;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public PropertyExpense Expense { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        // If reimbursed date is set but amount isn't, default to full expense amount.
        if (Expense.ReimbursedOn.HasValue && !Expense.ReimbursedAmount.HasValue)
            Expense.ReimbursedAmount = Expense.AmountDue;
        // Clear reimbursement fields if not pass-through (avoids leftover data).
        if (!Expense.PassThroughToTenant)
        {
            Expense.ReimbursedOn = null;
            Expense.ReimbursedAmount = null;
        }
        _db.PropertyExpenses.Add(Expense);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadAsync()
    {
        Properties = new SelectList(
            await _db.Properties.OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }
}
