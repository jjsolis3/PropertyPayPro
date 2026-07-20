using System.Text;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public static class EmailComposer
{
    public static string ComposeStatement(
        string baseUrl,
        Lease lease,
        IEnumerable<RentalCharge> unpaidCharges,
        DateOnly reportDate)
    {
        var rows = new StringBuilder();
        decimal totalDue = 0, totalPaid = 0, totalBalance = 0;
        foreach (var c in unpaidCharges.OrderBy(c => c.DueDate))
        {
            var statusBadge = c.Status switch
            {
                ChargeStatus.Overdue => "<span style=\"color:#c0392b;font-weight:bold;\">Overdue</span>",
                ChargeStatus.PartiallyPaid => "<span style=\"color:#d35400;\">Partial</span>",
                _ => "<span style=\"color:#7f8c8d;\">Unpaid</span>",
            };
            rows.Append($@"
                <tr>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{c.Id:D8}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{c.BillingPeriodStart:MMM yyyy}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{c.DueDate:yyyy-MM-dd}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{c.AmountDue:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{c.AmountPaid:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;font-weight:bold;"">{c.Balance:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{statusBadge}</td>
                </tr>");
            totalDue += c.AmountDue;
            totalPaid += c.AmountPaid;
            totalBalance += c.Balance;
        }

        var property = lease.Property!;
        var tenantNames = lease.TenantNames;
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";

        return BaseTemplate("Rental Account Statement", logoUrl, $@"
            <h1 style=""text-align:center;margin:8px 0 4px;color:#333;"">{Esc(property.Name)}</h1>
            <p style=""text-align:center;color:#666;margin:0 0 24px;"">Rental Account Statement</p>

            <p><strong>Report Date:</strong> {reportDate:MMMM d, yyyy}</p>

            <table style=""width:100%;border-collapse:collapse;margin:16px 0;"">
                <tr>
                    <td style=""width:50%;vertical-align:top;padding-right:16px;"">
                        <strong>Property</strong><br/>
                        {Esc(property.Name)}<br/>
                        {Esc(property.AddressLine1)}<br/>
                        {Esc(property.City)}, {Esc(property.State)} {Esc(property.PostalCode)}
                    </td>
                    <td style=""width:50%;vertical-align:top;border-left:1px solid #ddd;padding-left:16px;"">
                        <strong>Tenant(s)</strong><br/>
                        {Esc(tenantNames)}
                    </td>
                </tr>
            </table>

            <h3 style=""margin-top:24px;"">Unpaid Invoices</h3>
            <table style=""width:100%;border-collapse:collapse;font-size:14px;"">
                <thead style=""background:#f8f9fa;"">
                    <tr>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Invoice #</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Period</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Due Date</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Amount Due</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Paid</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Balance</th>
                        <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Status</th>
                    </tr>
                </thead>
                <tbody>{rows}</tbody>
                <tfoot style=""background:#f8f9fa;font-weight:bold;"">
                    <tr>
                        <td colspan=""3"" style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">TOTAL</td>
                        <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{totalDue:C}</td>
                        <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{totalPaid:C}</td>
                        <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;color:#c0392b;"">{totalBalance:C}</td>
                        <td style=""padding:8px;border:1px solid #dee2e6;""></td>
                    </tr>
                </tfoot>
            </table>

            <p style=""margin-top:24px;color:#555;"">
                Please remit payment for any outstanding balance at your earliest convenience.
                If you've already paid, please disregard this statement.
            </p>");
    }

    public static string ComposeReceipt(
        string baseUrl,
        RentPayment payment)
    {
        var lease = payment.Lease!;
        var property = lease.Property!;
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";

        var allocationRows = new StringBuilder();
        foreach (var a in payment.Allocations.OrderBy(a => a.RentalCharge?.BillingPeriodStart))
        {
            var c = a.RentalCharge!;
            allocationRows.Append($@"
                <tr>
                    <td style=""padding:6px;border-bottom:1px solid #eee;"">{c.PeriodLabel}</td>
                    <td style=""padding:6px;border-bottom:1px solid #eee;"">{c.DueDate:yyyy-MM-dd}</td>
                    <td style=""padding:6px;border-bottom:1px solid #eee;text-align:right;"">{a.Amount:C}</td>
                </tr>");
        }

        var allocationsSection = payment.Allocations.Any() ? $@"
            <h4 style=""margin-top:20px;"">Applied to</h4>
            <table style=""width:100%;border-collapse:collapse;font-size:14px;"">
                <thead style=""background:#f8f9fa;""><tr>
                    <th style=""padding:6px;text-align:left;border-bottom:2px solid #dee2e6;"">Period</th>
                    <th style=""padding:6px;text-align:left;border-bottom:2px solid #dee2e6;"">Due Date</th>
                    <th style=""padding:6px;text-align:right;border-bottom:2px solid #dee2e6;"">Applied</th>
                </tr></thead>
                <tbody>{allocationRows}</tbody>
                <tfoot><tr>
                    <td colspan=""2"" style=""padding:6px;text-align:right;font-weight:bold;"">Total applied</td>
                    <td style=""padding:6px;text-align:right;font-weight:bold;"">{payment.AllocatedAmount:C}</td>
                </tr>
                {(payment.UnallocatedAmount > 0 ? $@"<tr>
                    <td colspan=""2"" style=""padding:6px;text-align:right;color:#d35400;"">Unallocated (credit)</td>
                    <td style=""padding:6px;text-align:right;color:#d35400;"">{payment.UnallocatedAmount:C}</td>
                </tr>" : "")}
                </tfoot>
            </table>" : "<p style=\"color:#d35400;\">No allocations recorded — full amount held as credit on this lease.</p>";

        return BaseTemplate($"Receipt #{payment.Id}", logoUrl, $@"
            <h1 style=""text-align:center;margin:8px 0 4px;color:#333;"">{Esc(property.Name)}</h1>
            <p style=""text-align:center;color:#666;margin:0 0 24px;"">Rental Payment Receipt</p>

            <table style=""width:100%;border-collapse:collapse;"">
                <tr>
                    <td style=""width:50%;vertical-align:top;padding:8px 16px 8px 0;"">
                        <strong>Receipt #</strong> {payment.Id}<br/>
                        <strong>Date received:</strong> {payment.PaidOn:MMMM d, yyyy}<br/>
                        <strong>Method:</strong> {payment.Method}
                        {(string.IsNullOrWhiteSpace(payment.Reference) ? "" : $"<br/><strong>Ref:</strong> {Esc(payment.Reference)}")}
                    </td>
                    <td style=""width:50%;vertical-align:top;border-left:1px solid #ddd;padding:8px 0 8px 16px;"">
                        <strong>Tenant(s)</strong><br/>{Esc(lease.TenantNames)}<br/><br/>
                        <strong>Property</strong><br/>
                        {Esc(property.Name)}<br/>
                        {Esc(property.AddressLine1)}, {Esc(property.City)}
                    </td>
                </tr>
            </table>

            <div style=""background:#e8f5e9;border-left:4px solid #2e7d32;padding:12px 16px;margin:20px 0;font-size:18px;"">
                <strong>Amount received: {payment.Amount:C}</strong>
            </div>

            {allocationsSection}

            {(string.IsNullOrWhiteSpace(payment.Notes) ? "" : $"<p style=\"margin-top:16px;\"><strong>Notes:</strong> {Esc(payment.Notes)}</p>")}

            <p style=""margin-top:24px;color:#555;"">Thank you for your payment.</p>");
    }

    public static string ComposeReimbursementReminder(
        string baseUrl,
        Lease lease,
        IEnumerable<PropertyExpense> unreimbursedExpenses)
    {
        var rows = new StringBuilder();
        decimal totalDue = 0, totalReimb = 0, totalOutstanding = 0;
        foreach (var e in unreimbursedExpenses.OrderBy(x => x.DueDate))
        {
            rows.Append($@"
                <tr>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{Esc(e.Description)}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{Esc(e.Vendor ?? "")}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;"">{(e.DueDate?.ToString("yyyy-MM-dd") ?? "-")}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{e.AmountDue:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{(e.ReimbursedAmount ?? 0):C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;font-weight:bold;color:#b91c1c;"">{e.OutstandingReimbursement:C}</td>
                </tr>");
            totalDue += e.AmountDue;
            totalReimb += e.ReimbursedAmount ?? 0;
            totalOutstanding += e.OutstandingReimbursement;
        }

        var property = lease.Property!;
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";

        return BaseTemplate("Pass-through Expense Reimbursement", logoUrl, $@"
            <h1 style=""text-align:center;margin:8px 0 4px;color:#333;"">{Esc(property.Name)}</h1>
            <p style=""text-align:center;color:#666;margin:0 0 24px;"">Pass-through Expense Reimbursement Notice</p>

            <p>Hello {Esc(lease.TenantNames)},</p>
            <p>The following pass-through expenses are awaiting reimbursement per your lease terms:</p>

            <table style=""width:100%;border-collapse:collapse;font-size:14px;margin-top:12px;"">
                <thead style=""background:#f8f9fa;""><tr>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Expense</th>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Vendor</th>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:left;"">Due date</th>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Amount</th>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Reimbursed</th>
                    <th style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">Outstanding</th>
                </tr></thead>
                <tbody>{rows}</tbody>
                <tfoot style=""background:#f8f9fa;font-weight:bold;""><tr>
                    <td colspan=""3"" style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">TOTAL</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{totalDue:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;"">{totalReimb:C}</td>
                    <td style=""padding:8px;border:1px solid #dee2e6;text-align:right;color:#b91c1c;"">{totalOutstanding:C}</td>
                </tr></tfoot>
            </table>

            <p style=""margin-top:20px;"">Please remit the outstanding amount with your next rent payment.</p>");
    }

    public static string ComposeInvite(
        string baseUrl,
        string displayName,
        string resetLink,
        bool isTenant)
    {
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";
        var roleLabel = isTenant ? "tenant portal" : "administration portal";
        var welcomeLine = isTenant
            ? "Welcome! You've been invited to the PropertyPayPro tenant portal, where you can view your bills, payment history, and lease documents."
            : "Welcome! You've been invited to the PropertyPayPro admin portal.";

        return BaseTemplate($"You're invited to PropertyPayPro", logoUrl, $@"
            <h1 style=""text-align:center;margin:8px 0 4px;color:#333;"">Welcome, {Esc(displayName)}</h1>
            <p style=""text-align:center;color:#666;margin:0 0 24px;"">Set your password to activate your {roleLabel} account.</p>

            <p>{welcomeLine}</p>

            <p style=""text-align:center;margin:32px 0;"">
                <a href=""{resetLink}"" style=""display:inline-block;background:#2196f3;color:#fff;text-decoration:none;padding:12px 28px;border-radius:6px;font-weight:bold;font-size:16px;"">
                    Set your password
                </a>
            </p>

            <p style=""color:#555;font-size:14px;"">
                If the button above doesn't work, copy and paste this link into your browser:
            </p>
            <p style=""word-break:break-all;background:#f8f9fa;padding:12px;border-radius:4px;font-family:monospace;font-size:12px;color:#333;"">
                {Esc(resetLink)}
            </p>

            <p style=""color:#888;font-size:13px;margin-top:24px;"">
                This link expires within 24 hours per Identity defaults. If it stops working, ask your admin to re-issue it.
            </p>");
    }

    public static string ComposeNotice(
        string baseUrl,
        string noticeTitle,
        string pdfFileName)
    {
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";
        return BaseTemplate(noticeTitle, logoUrl, $@"
            <h1 style=""margin:8px 0 4px;color:#333;"">{Esc(noticeTitle)}</h1>
            <p style=""color:#666;margin:0 0 20px;"">A formal notice from your property management has been attached to this email.</p>

            <p>The full notice is included as a PDF attachment: <strong>{Esc(pdfFileName)}</strong>.</p>

            <p style=""margin-top:20px;color:#555;"">
                Please review the attached notice carefully. If you have questions or believe
                you have received it in error, contact your property manager as soon as possible.
            </p>");
    }

    public static string ComposeBroadcast(
        string baseUrl,
        string subject,
        string plainTextBody)
    {
        var logoUrl = $"{baseUrl}/img/brand/PPS_Logo_Main.png";
        // Preserve line breaks the admin typed by converting them to <br />
        // AFTER escaping — otherwise the escape swallows the newlines.
        var htmlBody = Esc(plainTextBody).Replace("\r\n", "\n").Replace("\n", "<br />\n");
        return BaseTemplate(subject, logoUrl, $@"
            <h1 style=""margin:8px 0 4px;color:#333;"">{Esc(subject)}</h1>
            <div style=""margin-top:20px;line-height:1.6;color:#333;"">
                {htmlBody}
            </div>");
    }

    private static string BaseTemplate(string title, string logoUrl, string innerHtml) => $@"<!doctype html>
<html><head><meta charset=""utf-8""><title>{title}</title></head>
<body style=""font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:#f5f5f5;margin:0;padding:24px;color:#333;"">
    <div style=""max-width:720px;margin:0 auto;background:#fff;border-radius:8px;padding:32px;box-shadow:0 2px 8px rgba(0,0,0,0.08);"">
        <div style=""text-align:center;margin-bottom:16px;"">
            <img src=""{logoUrl}"" alt=""PropertyPayPro"" style=""max-width:200px;height:auto;"" />
        </div>
        {innerHtml}
        <hr style=""border:none;border-top:1px solid #eee;margin:32px 0 16px;"" />
        <p style=""text-align:center;color:#999;font-size:12px;margin:0;"">Sent by PropertyPayPro</p>
    </div>
</body></html>";

    private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}
