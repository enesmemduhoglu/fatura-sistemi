using Isbasi.Web.Controllers;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Isbasi.Tests;

/// <summary>KDV raporu toplama (SMM + döviz) ve cari ekstre bakiye/devir doğruluğu.</summary>
public class ReportsAndStatementTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task KdvRaporu_SmmVeDovizliFaturayi_TlBazindaToplar()
    {
        var firm = TestData.AddFirm(_db.Context);
        int year = DateTime.Today.Year;
        var june = new DateTime(year, 6, 15);

        // TL satış: KDV 40 — USD satış: KDV 20 $ × kur 40 = 800 TL
        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 200, date: june);
        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100,
            currency: "USD", rate: 40, date: june);
        // Alış: indirilecek KDV 60 TL
        TestData.AddInvoice(_db.Context, InvoiceType.Purchase, firm, 300, date: june);
        // Verilen SMM: KDV %20 × 1.000 = 200 hesaplanan; alınan SMM: 100 indirilecek
        _db.Context.FreelanceReceipts.Add(new FreelanceReceipt
        {
            ReceiptNumber = "S1", Type = ReceiptType.Issued, FirmId = firm.Id,
            Date = june, GrossAmount = 1000, StopajRate = 20, VatRate = 20
        });
        _db.Context.FreelanceReceipts.Add(new FreelanceReceipt
        {
            ReceiptNumber = "S2", Type = ReceiptType.Received, FirmId = firm.Id,
            Date = june, GrossAmount = 500, StopajRate = 20, VatRate = 20
        });
        _db.Context.SaveChanges();

        var result = await new ReportsController(_db.Context).VatReport(year);
        var vm = Assert.IsType<VatReportViewModel>(Assert.IsType<ViewResult>(result).Model);
        var juneRow = vm.Months[5];

        Assert.Equal(1040m, juneRow.CalculatedVat);   // 40 + 800 + 200
        Assert.Equal(160m, juneRow.DeductibleVat);    // 60 + 100
        Assert.Equal(880m, juneRow.Difference);
    }

    [Fact]
    public async Task CariEkstre_YuruyenBakiye_TlBazinda()
    {
        var firm = TestData.AddFirm(_db.Context);
        var jan = new DateTime(2026, 1, 10);
        var feb = new DateTime(2026, 2, 10);

        // Oca: satış 240 TL (borç) — Şub: USD satış 120 $ × 40 = 4.800 TL (borç)
        var sale = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 200, date: jan);
        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100,
            currency: "USD", rate: 40, date: feb);
        // Oca: 240 TL tahsilat (alacak)
        _db.Context.Payments.Add(new Payment { InvoiceId = sale.Id, Amount = 240, Date = jan.AddDays(5) });
        _db.Context.SaveChanges();

        var result = await new DefinitionsController(_db.Context).FirmStatement(firm.Id, null, null);
        var vm = Assert.IsType<FirmStatementViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(3, vm.Rows.Count);
        Assert.Equal(5040m, vm.TotalDebit);           // 240 + 4.800 (TL karşılığı!)
        Assert.Equal(240m, vm.TotalCredit);
        Assert.Equal(4800m, vm.Balance);
        Assert.Equal(4800m, vm.Rows[^1].Balance);     // yürüyen bakiye son satırda
    }

    [Fact]
    public async Task CariEkstre_TarihFiltresi_DevirSatiriUretir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var sale = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 1000,
            vatRate: 0, date: new DateTime(2026, 1, 10));
        _db.Context.Payments.Add(new Payment { InvoiceId = sale.Id, Amount = 400, Date = new DateTime(2026, 1, 20) });
        _db.Context.SaveChanges();
        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 500,
            vatRate: 0, date: new DateTime(2026, 3, 5));

        // Şubat'tan itibaren: Ocak hareketleri (1000 borç - 400 alacak) devire toplanmalı
        var result = await new DefinitionsController(_db.Context)
            .FirmStatement(firm.Id, new DateTime(2026, 2, 1), null);
        var vm = Assert.IsType<FirmStatementViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(vm.HasOpening);
        Assert.Equal(600m, vm.OpeningBalance);
        Assert.Single(vm.Rows);                       // yalnız Mart faturası listelenir
        Assert.Equal(1100m, vm.Rows[0].Balance);      // 600 devir + 500
    }

    [Fact]
    public async Task CariEkstre_KarsiliksizCek_BakiyeyeGirmez()
    {
        var firm = TestData.AddFirm(_db.Context);
        _db.Context.Cheques.Add(new Cheque
        {
            Type = ChequeType.Received, FirmId = firm.Id, ChequeNumber = "C1",
            Amount = 5000, Status = ChequeStatus.Bounced,
            IssueDate = DateTime.Today, DueDate = DateTime.Today
        });
        _db.Context.SaveChanges();

        var result = await new DefinitionsController(_db.Context).FirmStatement(firm.Id, null, null);
        var vm = Assert.IsType<FirmStatementViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Empty(vm.Rows);
        Assert.Equal(0m, vm.Balance);
    }
}
