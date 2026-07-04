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

    [Fact]
    public void Stopaj_IndirimliKdvHaricToplamUzerindenKesilir()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 1000, VatRate = 20 });
        invoice.GeneralDiscountValue = 100;
        invoice.GeneralDiscountType = DiscountType.Amount;
        invoice.StopajRate = 20;
        InvoiceCalculator.Calculate(invoice);

        // Matrah 900 (1000 − 100 indirim); KDV oransal azalır: 200 × 0,9 = 180
        Assert.Equal(180m, invoice.StopajTotal);
        Assert.Equal(180m, invoice.VatTotal);
        Assert.Equal(900m + 180m - 180m, invoice.GrandTotal);
    }

    [Fact]
    public void Tevkifat_KdvninKesriniGenelToplamdanDuser_KdvToplamiTamKalir()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 1000, VatRate = 20 });
        invoice.TevkifatCode = "9/10";
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(200m, invoice.VatTotal);            // KDV toplamı değişmez
        Assert.Equal(180m, invoice.TevkifatVatTotal);    // 200 × 9/10
        Assert.Equal(1020m, invoice.GrandTotal);         // 1000 + 200 − 180
    }

    [Fact]
    public void StopajVeTevkifat_BirlikteUygulanir()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 1000, VatRate = 20 });
        invoice.StopajRate = 20;
        invoice.TevkifatCode = "5/10";
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(200m, invoice.StopajTotal);
        Assert.Equal(100m, invoice.TevkifatVatTotal);
        Assert.Equal(1000m + 200m - 200m - 100m, invoice.GrandTotal);
    }

    [Fact]
    public void GecersizTevkifatKodu_TemizlenirVeKesintiYapilmaz()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 100, VatRate = 20 });
        invoice.TevkifatCode = "7/9";   // listede olmayan kod
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal("", invoice.TevkifatCode);
        Assert.Equal(0m, invoice.TevkifatVatTotal);
        Assert.Equal(120m, invoice.GrandTotal);
    }

    [Fact]
    public void StopajOrani_SifirYuzAraligina_Sikistirilir()
    {
        var invoice = Wholesale(new InvoiceLine { Quantity = 1, UnitPrice = 100, VatRate = 20 });
        invoice.StopajRate = 150;
        InvoiceCalculator.Calculate(invoice);

        Assert.Equal(100m, invoice.StopajRate);
        Assert.Equal(100m, invoice.StopajTotal);
        Assert.Equal(20m, invoice.GrandTotal);   // 100 + 20 KDV − 100 stopaj

        invoice.StopajRate = -5;
        InvoiceCalculator.Calculate(invoice);
        Assert.Equal(0m, invoice.StopajRate);
        Assert.Equal(0m, invoice.StopajTotal);
    }
}
