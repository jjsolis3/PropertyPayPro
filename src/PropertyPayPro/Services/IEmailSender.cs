namespace PropertyPayPro.Services;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
}
