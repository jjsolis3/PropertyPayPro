using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Expenses;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public PropertyExpense? Expense { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Expense = await _db.PropertyExpenses
            .Include(e => e.Property)
            .FirstOrDefaultAsync(e => e.Id == id);
        return Expense is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostMarkReimbursedAsync(int id)
    {
        var e = await _db.PropertyExpenses.FindAsync(id);
        if (e is null) return NotFound();
        e.ReimbursedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        e.ReimbursedAmount = e.AmountDue;
        await _db.SaveChangesAsync();
        TempData["Message"] = "Marked as reimbursed in full.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostClearReimbursedAsync(int id)
    {
        var e = await _db.PropertyExpenses.FindAsync(id);
        if (e is null) return NotFound();
        e.ReimbursedOn = null;
        e.ReimbursedAmount = null;
        await _db.SaveChangesAsync();
        TempData["Message"] = "Reimbursement cleared.";
        return RedirectToPage(new { id });
    }
}
