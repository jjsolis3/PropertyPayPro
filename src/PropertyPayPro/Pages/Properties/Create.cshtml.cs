using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Property Property { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        _db.Properties.Add(Property);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
