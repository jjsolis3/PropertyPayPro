using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings.Templates;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly NoticeTemplateSeeder _seeder;
    private readonly IAuditLog _audit;

    public EditModel(ApplicationDbContext db, NoticeTemplateSeeder seeder, IAuditLog audit)
    {
        _db = db;
        _seeder = seeder;
        _audit = audit;
    }

    public class InputModel
    {
        public int Id { get; set; }
        [Required, StringLength(120)] public string Label { get; set; } = "";
        [StringLength(300)] public string? TitleOrSubject { get; set; }
        [Required] public string Body { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public NoticeTemplate? Template { get; private set; }
    public IReadOnlyList<string> SupportedTokens => NoticeTemplateService.SupportedTokens;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ViewData["SettingsTab"] = "templates";
        Template = await _db.NoticeTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (Template is null) return NotFound();

        Input.Id = Template.Id;
        Input.Label = Template.Label;
        Input.TitleOrSubject = Template.TitleOrSubject;
        Input.Body = Template.Body;
        Input.IsActive = Template.IsActive;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["SettingsTab"] = "templates";
        Template = await _db.NoticeTemplates.FirstOrDefaultAsync(t => t.Id == Input.Id);
        if (Template is null) return NotFound();
        if (!ModelState.IsValid) return Page();

        Template.Label = Input.Label;
        Template.TitleOrSubject = Input.TitleOrSubject;
        Template.Body = Input.Body;
        Template.IsActive = Input.IsActive;
        Template.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync("TemplateEdited",
            entityType: nameof(NoticeTemplate),
            entityId: Template.Id,
            details: $"{Template.Scope}/{Template.Slug}");

        TempData["Message"] = $"Template \"{Template.Label}\" saved.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetAsync(int id)
    {
        var row = await _db.NoticeTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (row is null) return NotFound();
        if (!row.IsSystem)
        {
            TempData["Error"] = "Only system templates can be reset.";
            return RedirectToPage(new { id });
        }
        var ok = await _seeder.ResetToDefaultAsync(id);
        if (ok)
        {
            await _audit.WriteAsync("TemplateReset",
                entityType: nameof(NoticeTemplate), entityId: id,
                details: $"{row.Scope}/{row.Slug}");
            TempData["Message"] = $"Template \"{row.Label}\" reset to the shipped default.";
        }
        else
        {
            TempData["Error"] = "Could not reset (no default on file).";
        }
        return RedirectToPage(new { id });
    }
}
