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
}
