namespace PropertyPayPro.Services;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken ct = default);
}
