using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public class MailService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _sender;
    private readonly PdfService _pdf;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<MailService> _logger;

    public MailService(
        ApplicationDbContext db,
        IEmailSender sender,
        PdfService pdf,
        IDocumentStorage storage,
        ILogger<MailService> logger)
    {
        _db = db;
        _sender = sender;
        _pdf = pdf;
        _storage = storage;
        _logger = logger;
    }

    public bool IsConfigured => _sender.IsConfigured;

    public async Task<EmailLog> SendStatementAsync(string baseUrl, int leaseId, CancellationToken ct = default)
    {
        var lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == leaseId, ct)
            ?? throw new InvalidOperationException($"Lease {leaseId} not found.");

        var tenant = lease.Tenants.FirstOrDefault(t => t.ReceiveReminders && !string.IsNullOrWhiteSpace(t.Email));
        if (tenant is null || string.IsNullOrWhiteSpace(tenant.Email))
        {
            return await LogAsync(EmailKind.Statement, EmailStatus.Failed, "(no eligible tenant email)",
                $"Statement for {lease.Property!.Name}", "No tenant on this lease has reminders enabled or an email on file.",
                leaseId: leaseId, ct: ct);
        }

        var unpaid = lease.Charges.Where(c => c.Balance > 0).ToList();
        var subject = $"Rental Statement — {lease.Property!.Name}";
        var body = EmailComposer.ComposeStatement(baseUrl, lease, unpaid, DateOnly.FromDateTime(DateTime.UtcNow));

        var pdfDoc = await _pdf.GenerateUnpaidStatementAsync(leaseId, ct);
        var attachments = await LoadAttachmentsAsync(pdfDoc, ct);

        return await TrySendAsync(EmailKind.Statement, tenant.Email!, subject, body, attachments,
            leaseId: leaseId, ct: ct);
    }

    public async Task<EmailLog> SendReceiptAsync(string baseUrl, int paymentId, CancellationToken ct = default)
    {
        var payment = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenants)
            .Include(p => p.Allocations).ThenInclude(a => a.RentalCharge)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new InvalidOperationException($"Payment {paymentId} not found.");

        var tenant = payment.Lease!.Tenants.FirstOrDefault(t => t.ReceiveReceipts && !string.IsNullOrWhiteSpace(t.Email));
        if (tenant is null || string.IsNullOrWhiteSpace(tenant.Email))
        {
            return await LogAsync(EmailKind.Receipt, EmailStatus.Failed, "(no eligible tenant email)",
                $"Receipt #{payment.Id}", "No tenant on this lease has receipts enabled or an email on file.",
                leaseId: payment.LeaseId, paymentId: payment.Id, ct: ct);
        }

        var subject = $"Payment Receipt #{payment.Id} — {payment.Lease!.Property!.Name}";
        var body = EmailComposer.ComposeReceipt(baseUrl, payment);

        var pdfDoc = await _pdf.GenerateReceiptAsync(paymentId, ct);
        var attachments = await LoadAttachmentsAsync(pdfDoc, ct);

        return await TrySendAsync(EmailKind.Receipt, tenant.Email!, subject, body, attachments,
            leaseId: payment.LeaseId, paymentId: payment.Id, ct: ct);
    }

    private async Task<List<EmailAttachment>> LoadAttachmentsAsync(GeneratedDocument doc, CancellationToken ct)
    {
        await using var stream = await _storage.OpenReadAsync(doc.StorageKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new List<EmailAttachment> { new(doc.FileName, "application/pdf", ms.ToArray()) };
    }

    private async Task<EmailLog> TrySendAsync(
        EmailKind kind, string to, string subject, string body,
        IReadOnlyList<EmailAttachment>? attachments = null,
        int? leaseId = null, int? paymentId = null, CancellationToken ct = default)
    {
        try
        {
            await _sender.SendAsync(to, subject, body, attachments, ct);
            return await LogAsync(kind, EmailStatus.Sent, to, subject, error: null,
                leaseId: leaseId, paymentId: paymentId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {Kind} email to {To}", kind, to);
            return await LogAsync(kind, EmailStatus.Failed, to, subject, ex.Message,
                leaseId: leaseId, paymentId: paymentId, ct: ct);
        }
    }

    private async Task<EmailLog> LogAsync(
        EmailKind kind, EmailStatus status, string to, string subject, string? error,
        int? leaseId = null, int? paymentId = null, CancellationToken ct = default)
    {
        var log = new EmailLog
        {
            Kind = kind,
            Status = status,
            ToAddress = to,
            Subject = subject,
            Error = error,
            LeaseId = leaseId,
            RentPaymentId = paymentId
        };
        _db.EmailLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }
}
