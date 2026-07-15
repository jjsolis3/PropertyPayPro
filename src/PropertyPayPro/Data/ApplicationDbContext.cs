using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Models;

namespace PropertyPayPro.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<RentPayment> RentPayments => Set<RentPayment>();
    public DbSet<RentalCharge> RentalCharges => Set<RentalCharge>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<LeaseDocument> LeaseDocuments => Set<LeaseDocument>();
    public DbSet<PropertyExpense> PropertyExpenses => Set<PropertyExpense>();
    public DbSet<ServiceTicket> ServiceTickets => Set<ServiceTicket>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Lease>()
            .HasOne(l => l.Property)
            .WithMany(p => p.Leases)
            .HasForeignKey(l => l.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Lease>()
            .HasMany(l => l.Tenants)
            .WithMany(t => t.Leases)
            .UsingEntity(j => j.ToTable("LeaseTenants"));

        builder.Entity<RentPayment>()
            .HasOne(p => p.Lease)
            .WithMany(l => l.Payments)
            .HasForeignKey(p => p.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentPayment>()
            .HasOne(p => p.PaidByTenant)
            .WithMany()
            .HasForeignKey(p => p.PaidByTenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RentalCharge>()
            .HasOne(c => c.Lease)
            .WithMany(l => l.Charges)
            .HasForeignKey(c => c.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentalCharge>()
            .HasIndex(c => new { c.LeaseId, c.BillingPeriodStart, c.Kind })
            .IsUnique();

        builder.Entity<PaymentAllocation>()
            .HasOne(a => a.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(a => a.RentPaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAllocation>()
            .HasOne(a => a.RentalCharge)
            .WithMany(c => c.Allocations)
            .HasForeignKey(a => a.RentalChargeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<LeaseDocument>()
            .HasOne(d => d.Lease)
            .WithMany(l => l.Documents)
            .HasForeignKey(d => d.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PropertyExpense>()
            .HasOne(e => e.Property)
            .WithMany()
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ServiceTicket>()
            .HasOne(t => t.Property)
            .WithMany()
            .HasForeignKey(t => t.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GeneratedDocument>()
            .HasOne(d => d.Lease).WithMany().HasForeignKey(d => d.LeaseId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<GeneratedDocument>()
            .HasOne(d => d.RentalCharge).WithMany().HasForeignKey(d => d.RentalChargeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<GeneratedDocument>()
            .HasOne(d => d.RentPayment).WithMany().HasForeignKey(d => d.RentPaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
