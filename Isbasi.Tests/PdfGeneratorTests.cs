using Isbasi.Web.Data;
using Isbasi.Web.Models;

namespace Isbasi.Tests;

/// <summary>InvoicePdfGenerator: geçerli PDF üretimi, Türkçe içerik ve hatalı ayar dayanıklılığı.</summary>
public class PdfGeneratorTests
{
    private static Invoice FullInvoice() => new()
    {
        InvoiceNumber = "ISB2026000000042",
        Type = InvoiceType.SalesWholesale,
        InvoiceDate = new DateTime(2026, 7, 5),
        DueDate = new DateTime(2026, 8, 5),
        Currency = "USD",
        ExchangeRate = 40.5m,
        Description = "Türkçe açıklama — ğüşiöçİĞÜŞÖÇ",
        DeliveryAddress = "Depo Şubesi, Kadıköy / İstanbul",
        Firm = new Firm { Name = "Örnek Müşteri A.Ş.", City = "İstanbul", TaxOffice = "Kadıköy", TaxNumber = "1234567890" },
        StopajRate = 20,
        StopajTotal = 200,
        TevkifatCode = "9/10",
        TevkifatVatTotal = 180,
        SubTotal = 1000,
        VatTotal = 200,
        GrandTotal = 820,
        Lines = new List<InvoiceLine>
        {
            new() { ItemName = "Danışmanlık Hizmeti", Quantity = 2, Unit = "Saat", UnitPrice = 500, VatRate = 20, VatAmount = 200, LineTotal = 1000 }
        }
    };

    [Fact]
    public void TamKapsamliFatura_GecerliPdfUretir()
    {
        var company = new CompanySettings
        {
            CompanyName = "Şirketim Ltd. Şti.",
            PrintTemplate = "modern",
            PrintAccentColor = "#1a73e8",
            PrintFooterNote = "Ödemelerinizi 30 gün içinde yapınız."
        };

        var bytes = InvoicePdfGenerator.Generate(FullInvoice(), company);

        Assert.True(bytes.Length > 1000, "PDF beklenmedik şekilde küçük.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void GecersizTemaRengi_VarsayilanaDusur_PdfYineUretilir()
    {
        var company = new CompanySettings { CompanyName = "X", PrintAccentColor = "kırmızı" };

        var bytes = InvoicePdfGenerator.Generate(FullInvoice(), company);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void FirmaAyariYokken_DeUretilir()
    {
        var invoice = FullInvoice();
        invoice.Currency = "TL";
        invoice.ExchangeRate = 1;

        var bytes = InvoicePdfGenerator.Generate(invoice, company: null);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
