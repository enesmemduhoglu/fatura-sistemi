using Isbasi.Web.Data;
using Isbasi.Web.Models;

namespace Isbasi.Tests;

public class InvoiceCalculatorTests
{
    private static Invoice Wholesale(params InvoiceLine[] lines) => new()
    {
        Type = InvoiceType.SalesWholesale,
        Lines = lines.ToList()
    };

    [Fact]
    public void ToptanSatis_KdvHaric_ToplamlarDogru()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 2, UnitPrice = 100, VatRate = 20 });
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(200m, invoice.SubTotal);
        Assert.Equal(40m, invoice.VatTotal);
        Assert.Equal(240m, invoice.GrandTotal);
    }

    [Fact]
    public void PerakendeSatis_KdvDahil_MatrahIcindenAyristirilir()
    {
        var invoice = new Invoice
        {
            Type = InvoiceType.SalesRetail,
            Lines = new List<InvoiceLine> { new() { Quantity = 1, UnitPrice = 120, VatRate = 20 } }
        };
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(100m, invoice.SubTotal);
        Assert.Equal(20m, invoice.VatTotal);
        Assert.Equal(120m, invoice.GrandTotal);
    }

    [Fact]
    public void SatirIndirimi_OranBazli_DogruDusulur()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 1000, VatRate = 20, DiscountValue = 10, DiscountType = DiscountType.Rate });
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(900m, invoice.SubTotal);
        Assert.Equal(100m, invoice.DiscountTotal);
        Assert.Equal(180m, invoice.VatTotal);
        Assert.Equal(1080m, invoice.GrandTotal);
    }

    [Fact]
    public void GenelIndirim_KdvyiOransalAzaltir()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 1000, VatRate = 20 });
        invoice.GeneralDiscountValue = 50;
        invoice.GeneralDiscountType = DiscountType.Rate;
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(1000m, invoice.SubTotal);
        Assert.Equal(500m, invoice.DiscountTotal);
        Assert.Equal(100m, invoice.VatTotal);       // 200 KDV, %50 oranında azalır
        Assert.Equal(600m, invoice.GrandTotal);
    }

    [Fact]
    public void GenelIndirim_AraToplamiAsamaz()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 100, VatRate = 20 });
        invoice.GeneralDiscountValue = 999;
        invoice.GeneralDiscountType = DiscountType.Amount;
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(0m, invoice.GrandTotal);
        Assert.True(invoice.GrandTotal >= 0);
    }
}
