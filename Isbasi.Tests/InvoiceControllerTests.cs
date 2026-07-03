using Isbasi.Web.Controllers;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Tests;

public class InvoiceControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private InvoiceController Controller() => new InvoiceController(_db.Context).WithTempData();

    [Fact]
    public async Task Kaydet_TlBelgede_KurBireZorlanir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var model = NewInvoiceModel(firm, currency: "TL", rate: 37);

        await Controller().Save(model, null);

        Assert.Equal(1m, _db.Context.Invoices.Single().ExchangeRate);
    }

    [Fact]
    public async Task Kaydet_DovizliBelgede_SifirKurReddedilir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var model = NewInvoiceModel(firm, currency: "USD", rate: 0);

        var controller = Controller();
        var result = await controller.Save(model, null);

        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ViewResult>(result);           // form hatayla geri döner
        Assert.Empty(_db.Context.Invoices);
    }

    [Fact]
    public async Task Kaydet_GecersizDoviz_TlyeDusurulur()
    {
        var firm = TestData.AddFirm(_db.Context);
        var model = NewInvoiceModel(firm, currency: "BTC", rate: 5);

        await Controller().Save(model, null);

        var saved = _db.Context.Invoices.Single();
        Assert.Equal("TL", saved.Currency);
        Assert.Equal(1m, saved.ExchangeRate);
    }

    [Fact]
    public async Task SiparisDonusumu_DovizVeKuruTasir_SiparisiFaturalandiYapar()
    {
        var firm = TestData.AddFirm(_db.Context);
        var order = TestData.AddInvoice(_db.Context, InvoiceType.SalesOrder, firm, 100,
            currency: "USD", rate: 40);

        await Controller().Convert(order.Id);

        var invoice = _db.Context.Invoices.Include(i => i.Lines).Single(i => i.Id != order.Id);
        Assert.Equal(InvoiceType.SalesWholesale, invoice.Type);
        Assert.Equal("USD", invoice.Currency);
        Assert.Equal(40m, invoice.ExchangeRate);
        Assert.Single(invoice.Lines);

        var updatedOrder = _db.Context.Invoices.Single(i => i.Id == order.Id);
        Assert.Equal(OrderStatus.Invoiced, updatedOrder.OrderState);
        Assert.Equal(invoice.Id, updatedOrder.ConvertedInvoiceId);
    }

    [Fact]
    public async Task FaturalanmisSiparis_TekrarDonusturulUmez()
    {
        var firm = TestData.AddFirm(_db.Context);
        var order = TestData.AddInvoice(_db.Context, InvoiceType.SalesOrder, firm, 100);

        await Controller().Convert(order.Id);
        int invoiceCountAfterFirst = _db.Context.Invoices.Count();

        await Controller().Convert(order.Id);   // ikinci deneme üretim yapmamalı

        Assert.Equal(invoiceCountAfterFirst, _db.Context.Invoices.Count());
    }

    [Fact]
    public async Task Kopyalama_YeniTaslakUretir_KaynagiDegistirmez()
    {
        var firm = TestData.AddFirm(_db.Context);
        var source = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 250,
            currency: "EUR", rate: 44);

        var result = await Controller().Copy(source.Id);

        var view = Assert.IsType<ViewResult>(result);
        var copy = Assert.IsType<Invoice>(view.Model);
        Assert.Equal(0, copy.Id);                       // kaydedilmemiş taslak
        Assert.Equal("EUR", copy.Currency);
        Assert.Equal(44m, copy.ExchangeRate);
        Assert.Equal(source.GrandTotal, copy.GrandTotal);
        Assert.Single(_db.Context.Invoices);            // veritabanına yazılmadı
    }

    private static Invoice NewInvoiceModel(Firm firm, string currency, decimal rate) => new()
    {
        Type = InvoiceType.SalesWholesale,
        FirmId = firm.Id,
        InvoiceDate = DateTime.Today,
        Currency = currency,
        ExchangeRate = rate,
        Lines = new List<InvoiceLine>
        {
            new() { ItemName = "Kalem", Quantity = 1, Unit = "Adet", UnitPrice = 100, VatRate = 20 }
        }
    };
}
