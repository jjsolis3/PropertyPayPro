using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Models;

namespace PropertyPayPro.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<RentPayment> RentPayments => Set<RentPayment>();
    public DbSet<RentalCharge> RentalCharges => Set<RentalCharge>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Lease>()
            .HasOne(l => l.Property)
            .WithMany(p => p.Leases)
            .HasForeignKey(l => l.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Lease>()
            .HasOne(l => l.Tenant)
            .WithMany(t => t.Leases)
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentPayment>()
            .HasOne(p => p.Lease)
            .WithMany(l => l.Payments)
            .HasForeignKey(p => p.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentalCharge>()
            .HasOne(c => c.Lease)
            .WithMany(l => l.Charges)
            .HasForeignKey(c => c.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RentalCharge>()
            .HasIndex(c => new { c.LeaseId, c.BillingPeriodStart })
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
    }
}
