using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Expenses;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public PropertyExpense Expense { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.PropertyExpenses.FindAsync(id);
        if (entity is null) return NotFound();
        Expense = entity;
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
        if (Expense.ReimbursedOn.HasValue && !Expense.ReimbursedAmount.HasValue)
            Expense.ReimbursedAmount = Expense.AmountDue;
        if (!Expense.PassThroughToTenant)
        {
            Expense.ReimbursedOn = null;
            Expense.ReimbursedAmount = null;
        }
        _db.Attach(Expense).State = EntityState.Modified;
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
