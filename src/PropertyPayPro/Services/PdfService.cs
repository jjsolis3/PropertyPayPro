using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services.Pdfs;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services;

public class PdfService
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfService> _logger;
    private byte[]? _logoBytesCache;

    static PdfService()
    {
        // QuestPDF Community license — free for orgs under $1M USD revenue.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfService(
        ApplicationDbContext db,
        IDocumentStorage storage,
        IWebHostEnvironment env,
        ILogger<PdfService> logger)
    {
        _db = db;
        _storage = storage;
        _env = env;
        _logger = logger;
    }

    public async Task<GeneratedDocument> GenerateBillAsync(int rentalChargeId, CancellationToken ct = default)
    {
        var charge = await LoadChargeAsync(rentalChargeId, ct);
        var bytes = BillPdfBuilder.Build(charge, GetLogoBytes());
        var fileName = $"Bill_{charge.BillingPeriodStart:yyyyMM}_Inv{charge.Id:D8}.pdf";
        return await PersistAsync(GeneratedDocumentKind.Bill, bytes, fileName, "pdfs/bills",
            leaseId: charge.LeaseId, rentalChargeId: charge.Id, ct: ct);
    }

    public async Task<GeneratedDocument> GenerateReceiptAsync(int rentPaymentId, CancellationToken ct = default)
    {
        var payment = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenants)
            .Include(p => p.Allocations).ThenInclude(a => a.RentalCharge)
            .FirstOrDefaultAsync(p => p.Id == rentPaymentId, ct)
            ?? throw new InvalidOperationException($"Payment {rentPaymentId} not found.");

        var bytes = ReceiptPdfBuilder.Build(payment, GetLogoBytes());
        var fileName = $"Receipt_{payment.PaidOn:yyyyMMdd}_Pay{payment.Id:D8}.pdf";
        return await PersistAsync(GeneratedDocumentKind.Receipt, bytes, fileName, "pdfs/receipts",
            leaseId: payment.LeaseId, rentPaymentId: payment.Id, ct: ct);
    }

    public async Task<GeneratedDocument> GenerateUnpaidStatementAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == leaseId, ct)
            ?? throw new InvalidOperationException($"Lease {leaseId} not found.");

        var unpaid = lease.Charges.Where(c => c.Balance > 0).OrderBy(c => c.DueDate).ToList();
        var bytes = UnpaidStatementPdfBuilder.Build(lease, unpaid, DateOnly.FromDateTime(DateTime.UtcNow), GetLogoBytes());
        var fileName = $"Statement_{DateTime.UtcNow:yyyyMMdd}_Lease{lease.Id:D6}.pdf";
        return await PersistAsync(GeneratedDocumentKind.UnpaidStatement, bytes, fileName, "pdfs/statements",
            leaseId: lease.Id, ct: ct);
    }

    public async Task<GeneratedDocument?> GenerateBillPaidConfirmationIfClosedAsync(int rentalChargeId, CancellationToken ct = default)
    {
        var charge = await LoadChargeAsync(rentalChargeId, ct);
        if (charge.Balance > 0) return null;  // Bill not actually paid in full.

        var allocations = await _db.PaymentAllocations
            .Where(a => a.RentalChargeId == rentalChargeId)
            .Include(a => a.Payment).ThenInclude(p => p!.Lease)
            .ToListAsync(ct);

        var bytes = BillPaidConfirmationPdfBuilder.Build(charge, allocations, GetLogoBytes());
        var fileName = $"BillPaid_{charge.BillingPeriodStart:yyyyMM}_Inv{charge.Id:D8}.pdf";
        return await PersistAsync(GeneratedDocumentKind.BillPaidConfirmation, bytes, fileName, "pdfs/bill-paid",
            leaseId: charge.LeaseId, rentalChargeId: charge.Id, ct: ct);
    }

    public async Task<(Stream Stream, string FileName)> OpenAsync(int generatedDocumentId, CancellationToken ct = default)
    {
        var doc = await _db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == generatedDocumentId, ct)
            ?? throw new InvalidOperationException($"GeneratedDocument {generatedDocumentId} not found.");
        var stream = await _storage.OpenReadAsync(doc.StorageKey, ct);
        return (stream, doc.FileName);
    }

    private async Task<RentalCharge> LoadChargeAsync(int id, CancellationToken ct)
    {
        return await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new InvalidOperationException($"RentalCharge {id} not found.");
    }

    private async Task<GeneratedDocument> PersistAsync(
        GeneratedDocumentKind kind, byte[] bytes, string fileName, string folder,
        int? leaseId = null, int? rentalChargeId = null, int? rentPaymentId = null,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream(bytes);
        var storageKey = await _storage.SaveAsync(folder, fileName, ms, ct);

        var doc = new GeneratedDocument
        {
            Kind = kind,
            LeaseId = leaseId,
            RentalChargeId = rentalChargeId,
            RentPaymentId = rentPaymentId,
            StorageKey = storageKey,
            FileName = fileName,
            SizeBytes = bytes.Length
        };
        _db.GeneratedDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Generated {Kind} PDF {File} ({Bytes} bytes)", kind, fileName, bytes.Length);
        return doc;
    }

    private byte[]? GetLogoBytes()
    {
        if (_logoBytesCache is not null) return _logoBytesCache;
        var path = Path.Combine(_env.WebRootPath ?? "wwwroot", "img", "brand", "PPS_Logo_Main.png");
        if (!File.Exists(path)) return null;
        _logoBytesCache = File.ReadAllBytes(path);
        return _logoBytesCache;
    }
}
