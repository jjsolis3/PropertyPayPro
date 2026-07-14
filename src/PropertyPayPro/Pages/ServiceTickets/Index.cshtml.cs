using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<ServiceTicket> Tickets { get; private set; } = new List<ServiceTicket>();

    public int OpenCount { get; private set; }
    public int ScheduledThisWeek { get; private set; }
    public decimal SpentThisMonth { get; private set; }
    public int AvgDaysToResolve { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var ninetyDaysAgo = today.AddDays(-90);

        Tickets = await _db.ServiceTickets
            .Include(t => t.Property)
            .OrderByDescending(t => t.ReportedOn)
            .ThenByDescending(t => t.Id)
            .ToListAsync();

        OpenCount = Tickets.Count(t =>
            t.Status == ServiceTicketStatus.Open || t.Status == ServiceTicketStatus.InProgress);

        ScheduledThisWeek = Tickets.Count(t =>
            t.Status != ServiceTicketStatus.Completed &&
            t.Status != ServiceTicketStatus.Cancelled &&
            t.ScheduledFor.HasValue &&
            t.ScheduledFor.Value >= today && t.ScheduledFor.Value <= weekEnd);

        SpentThisMonth = Tickets
            .Where(t => t.ResolvedOn.HasValue && t.ResolvedOn.Value >= firstOfMonth && t.Cost.HasValue)
            .Sum(t => t.Cost ?? 0m);

        var resolvedRecent = Tickets
            .Where(t => t.Status == ServiceTicketStatus.Completed
                        && t.ResolvedOn.HasValue
                        && t.ResolvedOn.Value >= ninetyDaysAgo)
            .ToList();
        if (resolvedRecent.Count > 0)
        {
            var totalDays = resolvedRecent.Sum(t =>
                (t.ResolvedOn!.Value.DayNumber - t.ReportedOn.DayNumber));
            AvgDaysToResolve = Math.Max(0, totalDays / resolvedRecent.Count);
        }
    }
}
