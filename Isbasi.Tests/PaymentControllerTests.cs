using Isbasi.Web.Controllers;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Isbasi.Tests;

/// <summary>
/// Tahsilat akışının regresyon testleri. "Kısmi tahsilatta faturanın yanlışlıkla
/// Ödendi işaretlenmesi" (EF fixup çift sayması, commit c42330e) buradan yakalanır.
/// </summary>
public class PaymentControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private PaymentController Controller() => new PaymentController(_db.Context).WithTempData();

    private async Task Pay(int invoiceId, decimal amount, int safeId)
    {
        // Her tahsilat gerçek akıştaki gibi taze controller ile yapılır
        var result = await Controller().Add(new Payment
        {
            InvoiceId = invoiceId,
            Amount = amount,
            AccountType = PaymentAccountType.Safe,
            SafeId = safeId
        });
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task KismiTahsilatlar_ToplamAltindayken_AcikKalir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        // 3.200 TL'lik fatura (KDV dahil perakende senaryosu: 2.666,67 + 533,33)
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesRetail, firm, 3200, vatRate: 20);

        await Pay(invoice.Id, 1000, safe.Id);
        Assert.Equal(InvoiceStatus.Open, Status(invoice.Id));

        // Regresyon: 1000 + 2000 = 3000 < 3200 iken çift sayma 5000 >= 3200 yapıyordu
        await Pay(invoice.Id, 2000, safe.Id);
        Assert.Equal(InvoiceStatus.Open, Status(invoice.Id));

        await Pay(invoice.Id, 200, safe.Id);
        Assert.Equal(InvoiceStatus.Paid, Status(invoice.Id));
    }

    [Fact]
    public async Task TekBuyukKismiTahsilat_OdendiYapmaz()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 10000, vatRate: 0);

        // Kalanın yarısından büyük tek tahsilat: çift sayılırsa 12.000 >= 10.000 olur
        await Pay(invoice.Id, 6000, safe.Id);

        Assert.Equal(InvoiceStatus.Open, Status(invoice.Id));
    }

    [Fact]
    public async Task DovizliFatura_DurumTlKarsiligiylaIzlenir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        // 100 $ + %20 KDV = 120 $, kur 40,5 → 4.860 TL
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100,
            currency: "USD", rate: 40.5m);

        // Belge dövizi tutarı kadar TL (120) ödendiğinde Ödendi OLMAMALI
        await Pay(invoice.Id, 120, safe.Id);
        Assert.Equal(InvoiceStatus.Open, Status(invoice.Id));

        await Pay(invoice.Id, 4740, safe.Id);   // toplam 4.860 TL
        Assert.Equal(InvoiceStatus.Paid, Status(invoice.Id));
    }

    [Fact]
    public async Task TahsilatSilinince_DurumYenidenHesaplanir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 1000, vatRate: 0);

        await Pay(invoice.Id, 1000, safe.Id);
        Assert.Equal(InvoiceStatus.Paid, Status(invoice.Id));

        var paymentId = _db.Context.Payments.Single().Id;
        await Controller().Delete(paymentId);

        Assert.Equal(InvoiceStatus.Open, Status(invoice.Id));
    }

    [Fact]
    public async Task Siparise_TahsilatGirilemez()
    {
        var firm = TestData.AddFirm(_db.Context);
        var order = TestData.AddInvoice(_db.Context, InvoiceType.SalesOrder, firm, 500);

        var result = await Controller().Add(order.Id);

        Assert.IsType<RedirectResult>(result);   // sipariş listesine geri yönlendirilir
        Assert.Empty(_db.Context.Payments);
    }

    private InvoiceStatus Status(int invoiceId)
    {
        using var fresh = _db.NewContext();
        return fresh.Invoices.Single(i => i.Id == invoiceId).Status;
    }
}
