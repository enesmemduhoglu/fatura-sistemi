using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Firm> Firms => Set<Firm>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Safe> Safes => Set<Safe>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Cheque> Cheques => Set<Cheque>();
    public DbSet<FreelanceReceipt> FreelanceReceipts => Set<FreelanceReceipt>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>()
            .HasMany(i => i.Lines)
            .WithOne(l => l.Invoice)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Firm)
            .WithMany(f => f.Invoices)
            .HasForeignKey(i => i.FirmId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Invoice>()
            .HasMany(i => i.Payments)
            .WithOne(p => p.Invoice)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kasa/banka silinirken üzerindeki hareketler korunur; silme controller'da engellenir
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Safe)
            .WithMany()
            .HasForeignKey(p => p.SafeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.BankAccount)
            .WithMany()
            .HasForeignKey(p => p.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Çekin bağlı olduğu firma/kasa/banka silinirken hareket korunur
        modelBuilder.Entity<Cheque>()
            .HasOne(c => c.Firm)
            .WithMany()
            .HasForeignKey(c => c.FirmId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cheque>()
            .HasOne(c => c.Safe)
            .WithMany()
            .HasForeignKey(c => c.SafeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cheque>()
            .HasOne(c => c.BankAccount)
            .WithMany()
            .HasForeignKey(c => c.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FreelanceReceipt>()
            .HasOne(r => r.Firm)
            .WithMany()
            .HasForeignKey(r => r.FirmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
