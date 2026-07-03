using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>
/// HTTP seviyesinde regresyonlar: görünüm/binding hataları (hidden alan sızıntısı,
/// sabit ₺ simgesi, sipariş sızması) ancak bu katmanda yakalanır.
/// Testler aynı factory'yi paylaşır; her test kendi verisini benzersiz kılar.
/// </summary>
public class WebIntegrationTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public WebIntegrationTests(IsbasiWebFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    private async Task<HttpClient> LoggedInClient()
    {
        var client = Client();
        await client.LoginAsync();
        return client;
    }

    [Fact]
    public async Task GirisYapilmadan_SayfalarLogineYonlendirir()
    {
        var response = await Client().GetAsync("/invoice/sales");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task YanlisParola_GirisYaptirmaz()
    {
        var client = Client();
        var response = await client.PostFormAsync("/account/login", "/account/login", new()
        {
            ["email"] = "eneshan034@gmail.com",
            ["password"] = "yanlis-parola"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // form hatayla geri döner
    }

    [Fact]
    public async Task SatisFaturasiFormu_TypeQueryParametresi_HiddenAlanaSizmaz()
    {
        // Regresyon (668bfe9): ?type=Gross, ModelState üzerinden hidden Type alanına
        // "Gross" yazdırıyor ve kayıt "The value 'Gross' is not valid" ile patlıyordu
        var client = await LoggedInClient();

        var gross = await client.GetStringAsync("/invoice/sales/edit?type=Gross");
        Assert.Contains("name=\"Type\" value=\"SalesWholesale\"", gross.Replace("  ", " "));
        Assert.DoesNotContain("value=\"Gross\"", gross);

        var net = await client.GetStringAsync("/invoice/sales/edit?type=Net");
        Assert.Contains("name=\"Type\" value=\"SalesRetail\"", net.Replace("  ", " "));
    }

    [Fact]
    public async Task FormPostuylaFaturaKaydi_TrKulturuyleBaglanir()
    {
        var client = await LoggedInClient();

        // Virgüllü ondalıklar (tr-TR) gerçek boru hattından geçer
        var response = await client.PostFormAsync("/invoice/save", "/invoice/sales/edit?type=Gross", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesWholesale",
            ["Currency"] = "USD",
            ["ExchangeRate"] = "40,50",
            ["FirmId"] = "1",
            ["InvoiceDate"] = "2026-07-03",
            ["Status"] = "Open",
            ["InvoiceNumber"] = "ENTG-DOVIZ-1",
            ["Lines[0].ItemName"] = "Entegrasyon Kalemi",
            ["Lines[0].Quantity"] = "2",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "10,50",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Listede belge dövizi simgesi ve TL karşılığı görünür
        var list = await client.GetStringAsync("/invoice/sales");
        Assert.Contains("ENTG-DOVIZ-1", list);
        Assert.Contains("25,20 $", list);            // 2 × 10,50 + %20 KDV
        Assert.Contains("1.020,60", list);           // 25,20 × 40,50 TL karşılığı
    }

    [Fact]
    public async Task StokHareketleri_SiparisGostermez_DovizSimgesiDogru()
    {
        var client = await LoggedInClient();

        // Ürünlü USD alış siparişi ver: stok hareketlerinde GÖRÜNMEMELİ
        var response = await client.PostFormAsync("/invoice/save", "/invoice/purchaseorders/edit", new()
        {
            ["Id"] = "0",
            ["Type"] = "PurchaseOrder",
            ["Currency"] = "USD",
            ["ExchangeRate"] = "40",
            ["FirmId"] = "4",
            ["InvoiceDate"] = "2026-07-03",
            ["Status"] = "Open",
            ["InvoiceNumber"] = "ENTG-SIP-1",
            ["Lines[0].ProductId"] = "2",
            ["Lines[0].ItemName"] = "Un 25Kg",
            ["Lines[0].Quantity"] = "10",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "8",
            ["Lines[0].VatRate"] = "1"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var stock = await client.GetStringAsync("/stock/transactions");
        Assert.DoesNotContain("ENTG-SIP-1", stock);
    }

    [Fact]
    public async Task CsvDisaAktarim_DovizKolonlariVar()
    {
        var client = await LoggedInClient();

        var csv = await client.GetStringAsync("/invoice/export?mode=Sales");

        Assert.Contains("Döviz;Kur;TL Karşılığı", csv);
        Assert.Contains("USD", csv);   // seed'deki dövizli fatura satırı
    }

    [Fact]
    public async Task SiparisCsv_SiparisKolonlariVar()
    {
        var client = await LoggedInClient();

        var csv = await client.GetStringAsync("/invoice/export?mode=Orders");

        Assert.StartsWith("﻿Sipariş No;Firma;Sipariş Tipi", csv);
        Assert.Contains(";Durum", csv);
        Assert.DoesNotContain("Tahsil Edilen", csv);
    }

    [Fact]
    public async Task DovizliFatura_TahsilatSayfasi_TlKarsiligiGosterir()
    {
        // Seed USD faturası: 1.000 $ + %20 KDV = 1.200 $, kur 41,25 → 49.500 TL
        var client = await LoggedInClient();
        var list = await client.GetStringAsync("/invoice/sales");
        var match = System.Text.RegularExpressions.Regex.Match(list, "/payment/add\\?invoiceId=(\\d+)");
        Assert.True(match.Success);

        // Seed USD faturasının tahsilat sayfasını bul (id'ler değişebilir, hepsini tara)
        bool found = false;
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(list, "/payment/add\\?invoiceId=(\\d+)"))
        {
            var page = await client.GetStringAsync(m.Value.Replace("&amp;", "&"));
            if (page.Contains("1.200,00 $"))
            {
                Assert.Contains("49.500,00", page);   // TL karşılığı
                found = true;
                break;
            }
        }
        Assert.True(found, "Seed USD faturasının tahsilat sayfası bulunamadı.");
    }

    [Fact]
    public async Task VadesiGecenFatura_ListedeIsaretlenir()
    {
        var client = await LoggedInClient();

        var response = await client.PostFormAsync("/invoice/save", "/invoice/sales/edit?type=Gross", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesWholesale",
            ["Currency"] = "TL",
            ["ExchangeRate"] = "1",
            ["FirmId"] = "1",
            ["InvoiceDate"] = "2026-01-05",
            ["DueDate"] = "2026-02-01",     // geçmiş vade, Açık durum
            ["Status"] = "Open",
            ["InvoiceNumber"] = "ENTG-VADE-1",
            ["Lines[0].ItemName"] = "Kalem",
            ["Lines[0].Quantity"] = "1",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "100",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var list = await client.GetStringAsync("/invoice/sales?q=ENTG-VADE-1");
        Assert.Contains("Vadesi geçti", list);
    }
}
