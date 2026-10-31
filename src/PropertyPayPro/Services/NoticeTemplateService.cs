using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

/// <summary>
/// Context used to resolve tokens like <c>{{PropertyName}}</c> at
/// render time. Fields are optional — if a template references one
/// that wasn't supplied, the literal <c>{{Token}}</c> is left in place
/// so the admin notices the missing data in their preview.
/// </summary>
public record TemplateContext(
    Lease? Lease = null,
    decimal? OutstandingAmount = null,
    string? NoticeTitle = null,
    DateOnly? InspectionDate = null,
    string? InspectionKind = null);

public record RenderedTemplate(string TitleOrSubject, string Body);

public class NoticeTemplateService
{
    private readonly ApplicationDbContext _db;
    private static readonly Regex TokenRe = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public NoticeTemplateService(ApplicationDbContext db) { _db = db; }

    public async Task<List<NoticeTemplate>> ListAsync(NoticeTemplateScope scope, CancellationToken ct = default) =>
        await _db.NoticeTemplates
            .Where(t => t.Scope == scope && t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Label)
            .ToListAsync(ct);

    public async Task<NoticeTemplate?> GetAsync(int id, CancellationToken ct = default) =>
        await _db.NoticeTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<NoticeTemplate?> GetBySlugAsync(
        NoticeTemplateScope scope, string slug, CancellationToken ct = default) =>
        await _db.NoticeTemplates
            .FirstOrDefaultAsync(t => t.Scope == scope && t.Slug == slug, ct);

    public RenderedTemplate Render(NoticeTemplate template, TemplateContext ctx)
    {
        var tokens = BuildTokenMap(ctx);
        var title = Substitute(template.TitleOrSubject ?? "", tokens);
        var body = Substitute(template.Body, tokens);
        return new RenderedTemplate(title, body);
    }

    private static string Substitute(string input, Dictionary<string, string> tokens) =>
        TokenRe.Replace(input, m =>
            tokens.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    private static Dictionary<string, string> BuildTokenMap(TemplateContext ctx)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TodayDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("MMMM d, yyyy"),
        };

        if (ctx.Lease is Lease lease)
        {
            map["TenantNames"] = lease.TenantNames;
            map["MonthlyRent"] = lease.MonthlyRent.ToString("C");
            map["LeaseStart"] = lease.StartDate.ToString("MMMM d, yyyy");
            map["LeaseEnd"] = lease.IsMonthToMonth
                ? "month-to-month"
                : lease.EndDate.ToString("MMMM d, yyyy");

            if (lease.Property is Property p)
            {
                map["PropertyName"] = p.Name;
                map["Address"] = $"{p.AddressLine1}, {p.City}, {p.State} {p.PostalCode}";
            }
        }

        if (ctx.OutstandingAmount.HasValue)
            map["OutstandingAmount"] = ctx.OutstandingAmount.Value.ToString("C");
        if (!string.IsNullOrEmpty(ctx.NoticeTitle))
            map["NoticeTitle"] = ctx.NoticeTitle!;
        if (ctx.InspectionDate.HasValue)
            map["InspectionDate"] = ctx.InspectionDate.Value.ToString("MMMM d, yyyy");
        if (!string.IsNullOrEmpty(ctx.InspectionKind))
            map["InspectionKind"] = ctx.InspectionKind!;

        return map;
    }

    /// <summary>Token names that render into text; shown as help in the editor.</summary>
    public static IReadOnlyList<string> SupportedTokens { get; } = new[]
    {
        "PropertyName", "Address", "TenantNames",
        "MonthlyRent", "OutstandingAmount",
        "LeaseStart", "LeaseEnd", "TodayDate",
        "NoticeTitle", "InspectionDate", "InspectionKind",
    };
}
