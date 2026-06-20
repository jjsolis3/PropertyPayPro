using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Expenses;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<PropertyExpense> Expenses { get; private set; } = new List<PropertyExpense>();

    public async Task OnGetAsync()
    {
        Expenses = await _db.PropertyExpenses
            .Include(e => e.Property)
            .OrderByDescending(e => e.DueDate)
            .ThenByDescending(e => e.Id)
            .ToListAsync();
    }
}
