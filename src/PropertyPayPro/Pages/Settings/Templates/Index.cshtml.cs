using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings.Templates;

/// <summary>
/// Admin-only list of all templates grouped by scope. Covered by the
/// /Settings folder AdminOnly policy — no explicit [Authorize] needed.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) { _db = db; }

    public List<NoticeTemplate> PdfBodies { get; private set; } = new();
    public List<NoticeTemplate> CoverEmails { get; private set; } = new();
    public List<NoticeTemplate> SystemReminders { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["SettingsTab"] = "templates";
        var all = await _db.NoticeTemplates
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Label)
            .ToListAsync();
        PdfBodies = all.Where(t => t.Scope == NoticeTemplateScope.PdfBody).ToList();
        CoverEmails = all.Where(t => t.Scope == NoticeTemplateScope.CoverEmail).ToList();
        SystemReminders = all.Where(t => t.Scope == NoticeTemplateScope.SystemReminder).ToList();
    }
}
