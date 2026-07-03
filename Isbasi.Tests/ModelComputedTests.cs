using Isbasi.Web.Models;

namespace Isbasi.Tests;

/// <summary>Modeldeki türetilmiş alanlar: TL karşılığı, kalan tutar, SMM ve çek hesapları.</summary>
public class ModelComputedTests
{
    [Fact]
    public void DovizliFatura_TlKarsiligi_KurlaCarpilir()
    {
        var invoice = new Invoice { GrandTotal = 120, VatTotal = 20, Currency = "USD", ExchangeRate = 40.5m };

        Assert.Equal(4860m, invoice.GrandTotalTry);
        Assert.Equal(810m, invoice.VatTotalTry);
    }

    [Fact]
    public void TlFatura_TlKarsiligi_AyniKalir()
    {
        var invoice = new Invoice { GrandTotal = 240, VatTotal = 40 };

        Assert.Equal(240m, invoice.GrandTotalTry);
        Assert.Equal(40m, invoice.VatTotalTry);
    }

    [Theory]
    [InlineData("TL", "₺")]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    public void ParaBirimiSimgesi_DogruDoner(string currency, string expected)
    {
        Assert.Equal(expected, new Invoice { Currency = currency }.CurrencySymbol);
    }

    [Fact]
    public void DovizliFatura_Kalan_TlBazlidir()
    {
        // 120 $ x 40,5 = 4.860 TL; 4.860 TL tahsilatla kalan sıfırlanmalı —
        // belge dövizi (120) ile TL tahsilat karşılaştırılırsa hata yakalanır
        var invoice = new Invoice
        {
            GrandTotal = 120, Currency = "USD", ExchangeRate = 40.5m,
            Payments = new List<Payment> { new() { Amount = 4860 } }
        };

        Assert.Equal(0m, invoice.RemainingTotal);
    }

    [Fact]
    public void SerbestMeslekMakbuzu_Net_BrutEksiStopajArtiKdv()
    {
        var receipt = new FreelanceReceipt { GrossAmount = 10000, StopajRate = 20, VatRate = 20 };

        Assert.Equal(2000m, receipt.StopajAmount);
        Assert.Equal(2000m, receipt.VatAmount);
        Assert.Equal(10000m, receipt.NetAmount);
    }

    [Fact]
    public void SerbestMeslekMakbuzu_KurusYuvarlamasi()
    {
        var receipt = new FreelanceReceipt { GrossAmount = 333.33m, StopajRate = 20, VatRate = 10 };

        Assert.Equal(66.67m, receipt.StopajAmount);
        Assert.Equal(33.33m, receipt.VatAmount);
        Assert.Equal(299.99m, receipt.NetAmount);
    }

    [Theory]
    [InlineData(RecurringFrequency.Weekly, 7)]
    [InlineData(RecurringFrequency.Monthly, 31)]    // 3 Oca -> 3 Şub
    [InlineData(RecurringFrequency.Quarterly, 90)]  // 3 Oca -> 3 Nis
    [InlineData(RecurringFrequency.Yearly, 365)]
    public void TekrarlamaPlani_DonemIlerletme(RecurringFrequency frequency, int expectedDays)
    {
        var plan = new RecurringPlan { Frequency = frequency };
        var start = new DateTime(2026, 1, 3);

        Assert.Equal(expectedDays, (plan.Advance(start) - start).Days);
    }

    [Fact]
    public void SiparisVeFaturaAyrimi_IsOrderDogru()
    {
        Assert.True(new Invoice { Type = InvoiceType.SalesOrder }.IsOrder);
        Assert.True(new Invoice { Type = InvoiceType.PurchaseOrder }.IsOrder);
        Assert.False(new Invoice { Type = InvoiceType.SalesWholesale }.IsOrder);
        Assert.False(new Invoice { Type = InvoiceType.Expense }.IsOrder);
    }
}
