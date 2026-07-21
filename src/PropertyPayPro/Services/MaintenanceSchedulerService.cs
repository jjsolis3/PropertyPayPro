using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

/// <summary>
/// Walks every active MaintenanceSchedule and generates a ServiceTicket
/// for each one whose next-due date is inside its lead window. Bumps
/// NextDueDate forward by RecurrenceMonths afterwards so the next cycle
/// picks up naturally. Optionally emails tenants at the property and
/// admins/managers as configured on each schedule.
///
/// Invoked from three places:
///   • /Maintenance/Index "Generate now" button (manual trigger).
///   • Every request to /Maintenance/Index (opportunistic — cheap
///     because most days nothing is due).
///   • POST /api/jobs/generate-preventive-maintenance for an external
///     cron / scheduled task to hit daily.
/// </summary>
public class MaintenanceSchedulerService
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<MaintenanceSchedulerService> _logger;

    public MaintenanceSchedulerService(
        ApplicationDbContext db,
        MailService mail,
        UserManager<ApplicationUser> users,
        ILogger<MaintenanceSchedulerService> logger)
    {
        _db = db;
        _mail = mail;
        _users = users;
        _logger = logger;
    }

    public record RunResult(int SchedulesEvaluated, int TicketsCreated, int NotificationsSent);

    public async Task<RunResult> GenerateDueTicketsAsync(
        string baseUrl,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var due = await _db.MaintenanceSchedules
            .Include(s => s.Property)
            .Where(s => s.Active)
            .ToListAsync(ct);
        // In-memory filter — LeadDays is a per-row column so translating
        // "NextDueDate <= today + LeadDays" server-side is awkward; the
        // list is small enough that scanning it here is fine.
        var actionable = due
            .Where(s => s.DaysUntilDue <= s.LeadDays)
            .Where(s => !s.LastGeneratedOn.HasValue || s.LastGeneratedOn.Value < s.NextDueDate)
            .ToList();

        var ticketsCreated = 0;
        var notificationsSent = 0;

        foreach (var s in actionable)
        {
            var ticket = new ServiceTicket
            {
                PropertyId = s.PropertyId,
                Category = s.Category,
                Status = ServiceTicketStatus.Open,
                Priority = ServicePriority.Normal,
                Title = s.Title,
                Description = s.Description,
                Vendor = s.Vendor,
                VendorPhone = s.VendorPhone,
                ReportedOn = today,
                ScheduledFor = s.NextDueDate,
                Notes = "Auto-generated from Maintenance Schedule #" + s.Id +
                        (string.IsNullOrWhiteSpace(s.Notes) ? "" : "\n" + s.Notes)
            };
            _db.ServiceTickets.Add(ticket);
            await _db.SaveChangesAsync(ct);

            s.LastGeneratedOn = today;
            s.LastServiceTicketId = ticket.Id;
            s.NextDueDate = AdvanceNextDueDate(s.NextDueDate, s.RecurrenceMonths);
            await _db.SaveChangesAsync(ct);

            ticketsCreated++;
            notificationsSent += await SendNotificationsAsync(baseUrl, s, ticket.ScheduledFor!.Value, ct);

            _logger.LogInformation(
                "Generated preventive ticket #{TicketId} from schedule #{ScheduleId} ({Title}) — next due now {Next}",
                ticket.Id, s.Id, s.Title, s.NextDueDate);
        }

        return new RunResult(due.Count, ticketsCreated, notificationsSent);
    }

    private async Task<int> SendNotificationsAsync(
        string baseUrl,
        MaintenanceSchedule schedule,
        DateOnly scheduledFor,
        CancellationToken ct)
    {
        if (!_mail.IsConfigured) return 0;
        var sent = 0;

        if (schedule.NotifyTenants)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var tenantEmails = await _db.Leases
                .Where(l => l.PropertyId == schedule.PropertyId
                    && l.StartDate <= today
                    && (l.IsMonthToMonth || l.EndDate >= today))
                .SelectMany(l => l.Tenants)
                .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                .Select(t => t.Email!)
                .Distinct()
                .ToListAsync(ct);
            foreach (var email in tenantEmails)
            {
                var log = await _mail.SendMaintenanceReminderAsync(
                    baseUrl, email, schedule, scheduledFor, forAdmin: false, ct);
                if (log.Status == EmailStatus.Sent) sent++;
            }
        }

        if (schedule.NotifyAdmins)
        {
            var adminUsers = await _users.GetUsersInRoleAsync(IdentitySeed.AdminRole);
            var managerUsers = await _users.GetUsersInRoleAsync(IdentitySeed.ManagerRole);
            var adminEmails = adminUsers.Concat(managerUsers)
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var email in adminEmails)
            {
                var log = await _mail.SendMaintenanceReminderAsync(
                    baseUrl, email, schedule, scheduledFor, forAdmin: true, ct);
                if (log.Status == EmailStatus.Sent) sent++;
            }
        }

        return sent;
    }

    /// <summary>
    /// Advances a date by N months. Clamps the day to the last valid
    /// day of the target month so "January 31 + 1 month" becomes
    /// February 28/29 rather than throwing.
    /// </summary>
    private static DateOnly AdvanceNextDueDate(DateOnly from, int months)
    {
        var target = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(months);
        var daysInMonth = DateTime.DaysInMonth(target.Year, target.Month);
        var day = Math.Min(from.Day, daysInMonth);
        return new DateOnly(target.Year, target.Month, day);
    }
}
