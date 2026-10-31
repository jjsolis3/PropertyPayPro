using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Serializable snapshot of a notice package that can be stored in
/// <see cref="ScheduledTask.PayloadJson"/> and replayed by the
/// background runner at the scheduled time.
/// </summary>
public record NoticeSendPayload(
    int LeaseId,
    int PrimaryNoticeId,
    int[] AttachmentIds,
    string[] Recipients,
    string Subject,
    string Body,
    string BaseUrl);

public class NoticeSendHandler : IScheduledTaskHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly MailService _mail;
    private readonly ILogger<NoticeSendHandler> _logger;

    public NoticeSendHandler(
        ApplicationDbContext db,
        IDocumentStorage storage,
        MailService mail,
        ILogger<NoticeSendHandler> logger)
    {
        _db = db;
        _storage = storage;
        _mail = mail;
        _logger = logger;
    }

    public ScheduledTaskKind Kind => ScheduledTaskKind.NoticeSend;

    public async Task HandleAsync(ScheduledTask task, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<NoticeSendPayload>(task.PayloadJson)
            ?? throw new InvalidOperationException("Payload could not be deserialized.");

        if (payload.AttachmentIds.Length == 0)
            throw new InvalidOperationException("Scheduled send has no attachments.");
        if (payload.Recipients.Length == 0)
            throw new InvalidOperationException("Scheduled send has no recipients.");

        var docs = await _db.LeaseDocuments
            .Where(d => payload.AttachmentIds.Contains(d.Id) && d.LeaseId == payload.LeaseId)
            .ToListAsync(ct);
        if (docs.Count == 0)
            throw new InvalidOperationException("Attachments no longer exist (deleted before send?).");

        var attachments = new List<EmailAttachment>();
        foreach (var d in docs)
        {
            await using var stream = await _storage.OpenReadAsync(d.StorageKey, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            attachments.Add(new EmailAttachment(d.FileName, d.ContentType, ms.ToArray()));
        }

        var result = await _mail.SendNoticePackageAsync(
            payload.BaseUrl, payload.LeaseId,
            payload.Recipients, payload.Subject, payload.Body,
            attachments, ct);

        _logger.LogInformation(
            "Scheduled notice {TaskId} delivered: sent={Sent} failed={Failed}",
            task.Id, result.Sent, result.Failed);

        if (result.Sent > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var d in docs)
            {
                d.SentCount += result.Sent;
                d.LastSentUtc = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        // If every recipient failed, surface it as a task failure so
        // retry/backoff kicks in.
        if (result.Failed > 0 && result.Sent == 0)
        {
            throw new InvalidOperationException(
                "All deliveries failed: " + string.Join(" | ", result.Errors.Take(3)));
        }
    }
}
