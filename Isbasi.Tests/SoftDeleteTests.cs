using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Tests;

/// <summary>Yumuşak silme mekaniği: sorgu filtresi, numara serisi ve navigation davranışları.</summary>
public class SoftDeleteTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SilinenFatura_SorgulardanGizlenir_CopKutusundanGorunur()
    {
        var firm = TestData.AddFirm(_db.Context);
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100);

        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;
        await _db.Context.SaveChangesAsync();

        using var fresh = _db.NewContext();
        Assert.Equal(0, await fresh.Invoices.CountAsync());
        Assert.Equal(1, await fresh.Invoices.IgnoreQueryFilters().CountAsync(i => i.IsDeleted));
    }

    [Fact]
    public async Task SilinenFaturaninNumarasi_YenidenUretilmez()
    {
        var firm = TestData.AddFirm(_db.Context);
        int year = DateTime.Today.Year;
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100);
        invoice.InvoiceNumber = $"ISB{year}000000001";
        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;
        await _db.Context.SaveChangesAsync();

        // Çöpteki belge seri sayımına dahildir; aynı numara tekrar verilirse
        // geri almada çift numara oluşurdu
        Assert.Equal($"ISB{year}000000002", await InvoiceNumbers.Next(_db.Context, "ISB"));
    }

    [Fact]
    public async Task SilinenTahsilat_FaturaninOdenenToplaminaSayilmaz()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100);
        _db.Context.Payments.AddRange(
            new Payment { InvoiceId = invoice.Id, Amount = 50, AccountType = PaymentAccountType.Safe, SafeId = safe.Id },
            new Payment { InvoiceId = invoice.Id, Amount = 70, AccountType = PaymentAccountType.Safe, SafeId = safe.Id, IsDeleted = true, DeletedAt = DateTime.Now });
        await _db.Context.SaveChangesAsync();

        // Include, sorgu filtresinden geçer: çöpteki tahsilat koleksiyona gelmez
        using var fresh = _db.NewContext();
        var loaded = await fresh.Invoices.Include(i => i.Payments).FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(50, loaded.PaidTotal);
    }

    [Fact]
    public async Task SilinenFatura_KasaBankaBakiyesindenDuser()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context, opening: 1000);
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100);
        _db.Context.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id, Amount = 120, AccountType = PaymentAccountType.Safe, SafeId = safe.Id
        });
        await _db.Context.SaveChangesAsync();

        // Fatura tahsilatıyla birlikte çöpe taşınınca bakiye açılışa döner
        invoice.IsDeleted = true;
        invoice.DeletedAt = DateTime.Now;
        foreach (var p in invoice.Payments) { p.IsDeleted = true; p.DeletedAt = DateTime.Now; }
        await _db.Context.SaveChangesAsync();

        using var fresh = _db.NewContext();
        var balances = await CashBalanceCalculator.Compute(fresh);
        Assert.Equal(1000, balances.SafeBalances[safe.Id]);
    }
}
