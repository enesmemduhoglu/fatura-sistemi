using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>
/// Yumuşak silme ve çöp kutusu döngüsü gerçek HTTP boru hattıyla:
/// sil → listeden düşer, çöpte görünür → geri al / kalıcı sil.
/// </summary>
public class TrashTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public TrashTests(IsbasiWebFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await client.LoginAsync();
        return client;
    }

    private static async Task<int> CreateInvoice(HttpClient client, string number, string firmId = "1")
    {
        var save = await client.PostFormAsync("/invoice/save", "/invoice/sales/edit?type=Gross", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesWholesale",
            ["Currency"] = "TL",
            ["ExchangeRate"] = "1",
            ["FirmId"] = firmId,
            ["InvoiceDate"] = "2026-07-05",
            ["Status"] = "Open",
            ["InvoiceNumber"] = number,
            ["Lines[0].ItemName"] = "Çöp Testi Kalemi",
            ["Lines[0].Quantity"] = "1",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "250",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var list = await client.GetStringAsync($"/invoice/sales?q={number}");
        var match = Regex.Match(list, "/invoice/delete/(\\d+)");
        Assert.True(match.Success, "Kaydedilen fatura listede bulunamadı.");
        return int.Parse(match.Groups[1].Value);
    }

    [Fact]
    public async Task SilinenFatura_CopKutusunaGider_GeriAlinabilir()
    {
        var client = await LoggedInClient();
        int id = await CreateInvoice(client, "ENTG-TRASH-1");

        var delete = await client.PostFormAsync($"/invoice/delete/{id}",
            $"/invoice/sales/edit?id={id}", new());
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        // Listeden düşer (arama terimi kutuda yankılandığından satır, silme linkiyle aranır)
        Assert.DoesNotContain($"/invoice/delete/{id}", await client.GetStringAsync("/invoice/sales?q=ENTG-TRASH-1"));
        Assert.Contains("ENTG-TRASH-1", await client.GetStringAsync("/trash"));

        // Geri alınınca listeye döner, çöpten kalkar
        var restore = await client.PostFormAsync($"/trash/restore/invoice/{id}", "/trash", new());
        Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
        Assert.Contains($"/invoice/delete/{id}", await client.GetStringAsync("/invoice/sales?q=ENTG-TRASH-1"));
        // İlk GET, numarayı içeren TempData başarı mesajını tüketti; tablo artık temiz olmalı
        Assert.DoesNotContain("ENTG-TRASH-1", await client.GetStringAsync("/trash"));
    }

    [Fact]
    public async Task KaliciSilme_CopKutusundanDaKaldirir()
    {
        var client = await LoggedInClient();
        int id = await CreateInvoice(client, "ENTG-TRASH-2");

        await client.PostFormAsync($"/invoice/delete/{id}", $"/invoice/sales/edit?id={id}", new());
        var purge = await client.PostFormAsync($"/trash/purge/invoice/{id}", "/trash", new());
        Assert.Equal(HttpStatusCode.Redirect, purge.StatusCode);

        await client.GetStringAsync("/trash");   // numarayı içeren TempData mesajını tüketir
        Assert.DoesNotContain("ENTG-TRASH-2", await client.GetStringAsync("/trash"));
        Assert.DoesNotContain($"/invoice/delete/{id}", await client.GetStringAsync("/invoice/sales?q=ENTG-TRASH-2"));
    }

    [Fact]
    public async Task CoptekiFaturaninFirmasi_Silinemez()
    {
        var client = await LoggedInClient();

        // Kendi firması: başka faturası olmadığından koruma yalnızca çöpteki belgeden gelir
        var firmSave = await client.PostFormAsync("/definitions/firm/edit", "/definitions/firm/edit", new()
        {
            ["Id"] = "0",
            ["Name"] = "TrashTestFirma",
            ["Kind"] = "Corporate",
            ["IsCustomer"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, firmSave.StatusCode);

        var firmList = await client.GetStringAsync("/definitions/firm?q=TrashTestFirma");
        int firmId = int.Parse(Regex.Match(firmList, "/definitions/firm/delete/(\\d+)").Groups[1].Value);

        int invoiceId = await CreateInvoice(client, "ENTG-TRASH-3", firmId.ToString());
        await client.PostFormAsync($"/invoice/delete/{invoiceId}",
            $"/invoice/sales/edit?id={invoiceId}", new());

        // Çöpteki fatura firmayı kilitler; geri alma firmasız kalmasın
        var firmDelete = await client.PostFormAsync($"/definitions/firm/delete/{firmId}",
            "/definitions/firm?q=TrashTestFirma", new());
        Assert.Equal(HttpStatusCode.Redirect, firmDelete.StatusCode);
        // Arama terimi kutuda yankılanır; satırın varlığı silme linkiyle doğrulanır
        Assert.Contains($"/definitions/firm/delete/{firmId}",
            await client.GetStringAsync("/definitions/firm?q=TrashTestFirma"));
    }
}
