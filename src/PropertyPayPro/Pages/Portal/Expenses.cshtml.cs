using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

public class ExpensesModel : PortalPageBase
{
    public ExpensesModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
        : base(db, users) { }

    public List<PropertyExpense> Expenses { get; private set; } = new();
    public decimal OutstandingTotal { get; private set; }
    public decimal ReimbursedTotal { get; private set; }
    public int OutstandingCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        var propertyIds = AllLeases.Select(l => l.PropertyId).Distinct().ToList();

        Expenses = await Db.PropertyExpenses
            .Where(e => e.PassThroughToTenant && propertyIds.Contains(e.PropertyId))
            .Include(e => e.Property)
            .OrderByDescending(e => e.DueDate ?? DateOnly.MinValue)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        OutstandingTotal = Expenses.Sum(e => e.OutstandingReimbursement);
        ReimbursedTotal = Expenses.Where(e => e.IsReimbursed).Sum(e => e.ReimbursedAmount ?? 0m);
        OutstandingCount = Expenses.Count(e => e.OutstandingReimbursement > 0);

        return Page();
    }
}
