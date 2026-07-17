using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

public class ServicesModel : PortalPageBase
{
    public ServicesModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
        : base(db, users) { }

    public List<ServiceTicket> Tickets { get; private set; } = new();
    public int OpenCount { get; private set; }
    public int InProgressCount { get; private set; }
    public int CompletedCount { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        var propertyIds = AllLeases.Select(l => l.PropertyId).Distinct().ToList();

        Tickets = await Db.ServiceTickets
            .Where(t => propertyIds.Contains(t.PropertyId))
            .Include(t => t.Property)
            .OrderByDescending(t => t.ReportedOn).ThenByDescending(t => t.Id)
            .ToListAsync();

        OpenCount = Tickets.Count(t => t.Status == ServiceTicketStatus.Open);
        InProgressCount = Tickets.Count(t => t.Status == ServiceTicketStatus.InProgress);
        CompletedCount = Tickets.Count(t => t.Status == ServiceTicketStatus.Completed);

        return Page();
    }
}
