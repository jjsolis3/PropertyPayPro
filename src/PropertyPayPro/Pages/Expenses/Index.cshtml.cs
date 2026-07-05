using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public decimal SpentThisMonth { get; private set; }
    public decimal SpentYearToDate { get; private set; }
    public int OpenCount { get; private set; }
    public decimal OpenTotal { get; private set; }
    public string TopCategoryLabel { get; private set; } = "—";
    public decimal TopCategoryAmount { get; private set; }
    public decimal UnreimbursedTotal { get; private set; }
    public int UnreimbursedCount { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var firstOfYear = new DateOnly(today.Year, 1, 1);

        Expenses = await _db.PropertyExpenses
            .Include(e => e.Property)
            .OrderByDescending(e => e.DueDate)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        SpentThisMonth = Expenses
            .Where(e => e.PaidOn.HasValue && e.PaidOn.Value >= firstOfMonth)
            .Sum(e => e.AmountDue);

        SpentYearToDate = Expenses
            .Where(e => e.PaidOn.HasValue && e.PaidOn.Value >= firstOfYear)
            .Sum(e => e.AmountDue);

        var open = Expenses.Where(e => !e.IsPaid).ToList();
        OpenCount = open.Count;
        OpenTotal = open.Sum(e => e.AmountDue);

        var ytd = Expenses
            .Where(e => e.PaidOn.HasValue && e.PaidOn.Value >= firstOfYear)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.AmountDue) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();
        if (ytd is not null)
        {
            TopCategoryLabel = ytd.Category.ToString();
            TopCategoryAmount = ytd.Total;
        }

        var unreimbursed = Expenses.Where(e => e.PassThroughToTenant && e.OutstandingReimbursement > 0).ToList();
        UnreimbursedCount = unreimbursed.Count;
        UnreimbursedTotal = unreimbursed.Sum(e => e.OutstandingReimbursement);
    }

    public async Task<IActionResult> OnPostMarkReimbursedAsync(int id)
    {
        var e = await _db.PropertyExpenses.FindAsync(id);
        if (e is null) return NotFound();
        e.ReimbursedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        e.ReimbursedAmount = e.AmountDue;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Marked reimbursed: {e.Category} — {e.Vendor}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkSelectedReimbursedAsync(int[] selectedIds)
    {
        if (selectedIds is null || selectedIds.Length == 0)
        {
            TempData["Error"] = "No expenses selected.";
            return RedirectToPage();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expenses = await _db.PropertyExpenses
            .Where(e => selectedIds.Contains(e.Id) && e.PassThroughToTenant && e.ReimbursedOn == null)
            .ToListAsync();

        foreach (var e in expenses)
        {
            e.ReimbursedOn = today;
            e.ReimbursedAmount = e.AmountDue;
        }
        await _db.SaveChangesAsync();

        TempData["Message"] = expenses.Count switch
        {
            0 => "No eligible pass-through expenses were selected.",
            1 => "1 expense marked reimbursed.",
            _ => $"{expenses.Count} expenses marked reimbursed."
        };
        return RedirectToPage();
    }
}
