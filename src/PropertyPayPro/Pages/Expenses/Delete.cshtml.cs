using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Expenses;

[Authorize]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public PropertyExpense Expense { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.PropertyExpenses.FindAsync(id);
        if (entity is null) return NotFound();
        Expense = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.PropertyExpenses.FindAsync(Expense.Id);
        if (entity is null) return RedirectToPage("Index");
        _db.PropertyExpenses.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
