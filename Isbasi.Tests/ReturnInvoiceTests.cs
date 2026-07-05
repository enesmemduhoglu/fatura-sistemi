using Isbasi.Web.Controllers;
using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Isbasi.Tests;

/// <summary>
/// İade faturaları (Faz 17): stok/kasa yönleri, IAD serisi, iade taslağı oluşturma,
/// cari ekstre ve KDV raporu düzeltmeleri.
/// </summary>
public class ReturnInvoiceTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private InvoiceController Controller() => new InvoiceController(_db.Context).WithTempData();

    [Fact]
    public void NakitYonu_SatisIadesiOdeme_AlisIadesiTahsilat()
    {
        Assert.True(new Invoice { Type = InvoiceType.SalesWholesale }.IsCashIncoming);
        Assert.False(new Invoice { Type = InvoiceType.Purchase }.IsCashIncoming);
        Assert.False(new Invoice { Type = InvoiceType.SalesReturn }.IsCashIncoming);
        Assert.True(new Invoice { Type = InvoiceType.PurchaseReturn }.IsCashIncoming);
        Assert.True(new Invoice { Type = InvoiceType.SalesReturn }.IsReturn);
        Assert.False(new Invoice { Type = InvoiceType.SalesReturn }.IsSales);
    }

    [Fact]
    public async Task Stok_SatisIadesiArtirir_AlisIadesiDusurur()
    {
        var firm = TestData.AddFirm(_db.Context);
        var product = TestData.AddProduct(_db.Context, stock: 100);

        TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 100, quantity: 10, product: product);
        TestData.AddInvoice(_db.Context, InvoiceType.PurchaseReturn, firm, 60, quantity: 4, product: product);

        var stocks = await StockCalculator.Compute(_db.Context);

        Assert.Equal(106m, stocks[product.Id]);   // 100 + 10 - 4
    }

    [Fact]
    public async Task KasaBakiyesi_SatisIadesiOdemesiDuser_AlisIadesiTahsilatiArtar()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context, opening: 1000);
        var salesReturn = TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 200);
        var purchaseReturn = TestData.AddInvoice(_db.Context, InvoiceType.PurchaseReturn, firm, 100);

        _db.Context.Payments.Add(new Payment { InvoiceId = salesReturn.Id, Amount = 240, AccountType = PaymentAccountType.Safe, SafeId = safe.Id });
        _db.Context.Payments.Add(new Payment { InvoiceId = purchaseReturn.Id, Amount = 120, AccountType = PaymentAccountType.Safe, SafeId = safe.Id });
        _db.Context.SaveChanges();

        var balances = await CashBalanceCalculator.Compute(_db.Context);

        Assert.Equal(880m, balances.SafeBalances[safe.Id]);   // 1000 - 240 + 120
    }

    [Fact]
    public async Task IadeTaslagi_TipiCevirir_SatirVeKuruTasir_VeritabaninaYazmaz()
    {
        var firm = TestData.AddFirm(_db.Context);
        var sale = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 250,
            currency: "USD", rate: 40);

        var result = await Controller().CreateReturn(sale.Id);

        var view = Assert.IsType<ViewResult>(result);
        var draft = Assert.IsType<Invoice>(view.Model);
        Assert.Equal(0, draft.Id);                            // kaydedilmemiş taslak
        Assert.Equal(InvoiceType.SalesReturn, draft.Type);
        Assert.Equal("USD", draft.Currency);
        Assert.Equal(40m, draft.ExchangeRate);
        Assert.Equal(sale.GrandTotal, draft.GrandTotal);
        Assert.Single(draft.Lines);
        Assert.Contains(sale.InvoiceNumber, draft.Description);
        Assert.Single(_db.Context.Invoices);                  // veritabanına yazılmadı
    }

    [Fact]
    public async Task IadeTaslagi_AlisVeGiderFaturasindan_AlisIadesiUretir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var purchase = TestData.AddInvoice(_db.Context, InvoiceType.Purchase, firm, 300);

        var result = await Controller().CreateReturn(purchase.Id);

        var draft = Assert.IsType<Invoice>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(InvoiceType.PurchaseReturn, draft.Type);
    }

    [Fact]
    public async Task IadeVeSiparisten_IadeOlusturulamaz()
    {
        var firm = TestData.AddFirm(_db.Context);
        var salesReturn = TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 100);
        var order = TestData.AddInvoice(_db.Context, InvoiceType.SalesOrder, firm, 100);

        Assert.IsType<RedirectToActionResult>(await Controller().CreateReturn(salesReturn.Id));
        Assert.IsType<RedirectToActionResult>(await Controller().CreateReturn(order.Id));
        Assert.Equal(2, _db.Context.Invoices.Count());        // yeni belge üretilmedi
    }

    [Fact]
    public async Task Kaydet_IadeFaturasi_IadSerisindenNumaraAlir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var model = new Invoice
        {
            Type = InvoiceType.SalesReturn,
            FirmId = firm.Id,
            InvoiceDate = DateTime.Today,
            Lines = new List<InvoiceLine>
            {
                new() { ItemName = "Kalem", Quantity = 1, Unit = "Adet", UnitPrice = 100, VatRate = 20 }
            }
        };

        await Controller().Save(model, null);

        Assert.StartsWith("IAD", _db.Context.Invoices.Single().InvoiceNumber);
    }

    [Fact]
    public async Task CariEkstre_SatisIadesiAlacak_AlisIadesiBorc()
    {
        var firm = TestData.AddFirm(_db.Context);
        TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 200);    // 240 TL alacak
        TestData.AddInvoice(_db.Context, InvoiceType.PurchaseReturn, firm, 100); // 120 TL borç

        var result = await new DefinitionsController(_db.Context).FirmStatement(firm.Id, null, null);
        var vm = Assert.IsType<FirmStatementViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(120m, vm.TotalDebit);
        Assert.Equal(240m, vm.TotalCredit);
        Assert.Equal(-120m, vm.Balance);                      // firma bizden alacaklı
    }

    [Fact]
    public async Task KdvRaporu_IadeKdvsi_KarsiTarafaYazilir()
    {
        var firm = TestData.AddFirm(_db.Context);
        int year = DateTime.Today.Year;
        var june = new DateTime(year, 6, 15);

        // Satış iadesi KDV 20 → indirilecek; alış iadesi KDV 40 → hesaplanan
        TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 100, date: june);
        TestData.AddInvoice(_db.Context, InvoiceType.PurchaseReturn, firm, 200, date: june);

        var result = await new ReportsController(_db.Context).VatReport(year);
        var vm = Assert.IsType<VatReportViewModel>(Assert.IsType<ViewResult>(result).Model);
        var juneRow = vm.Months[5];

        Assert.Equal(40m, juneRow.CalculatedVat);
        Assert.Equal(20m, juneRow.DeductibleVat);
    }

    [Fact]
    public async Task AlisSatisRaporu_IadeSutunlari_FarkaYansir()
    {
        var firm = TestData.AddFirm(_db.Context);
        int year = DateTime.Today.Year;
        var june = new DateTime(year, 6, 15);

        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 1000, vatRate: 0, date: june);
        TestData.AddInvoice(_db.Context, InvoiceType.SalesReturn, firm, 200, vatRate: 0, date: june);
        TestData.AddInvoice(_db.Context, InvoiceType.Purchase, firm, 400, vatRate: 0, date: june);
        TestData.AddInvoice(_db.Context, InvoiceType.PurchaseReturn, firm, 100, vatRate: 0, date: june);

        var result = await new ReportsController(_db.Context).SalesPurchase(year);
        var vm = Assert.IsType<SalesPurchaseViewModel>(Assert.IsType<ViewResult>(result).Model);
        var juneRow = vm.Months[5];

        Assert.Equal(1000m, juneRow.Sales);
        Assert.Equal(200m, juneRow.SalesReturns);
        Assert.Equal(400m, juneRow.Purchases);
        Assert.Equal(100m, juneRow.PurchaseReturns);
        Assert.Equal(500m, juneRow.Difference);               // (1000-200) - (400-100)
    }
}
