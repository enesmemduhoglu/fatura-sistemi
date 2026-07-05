using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>
/// İade faturası akışı gerçek HTTP boru hattıyla: formdan iade kaydı (IAD serisi),
/// faturadan iade taslağı açma ve iade listelerinin ayrışması.
/// </summary>
public class ReturnFlowTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public ReturnFlowTests(IsbasiWebFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await client.LoginAsync();
        return client;
    }

    [Fact]
    public async Task SatisIadesi_FormdanKaydedilir_IadSerisiyleListelenir()
    {
        var client = await LoggedInClient();

        // Numara boş bırakılır: IAD serisinden otomatik verilmeli
        var response = await client.PostFormAsync("/invoice/save", "/invoice/salesreturns/edit", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesReturn",
            ["Currency"] = "TL",
            ["ExchangeRate"] = "1",
            ["FirmId"] = "1",
            ["InvoiceDate"] = "2026-07-05",
            ["Status"] = "Open",
            ["InvoiceNumber"] = "",
            ["Description"] = "ENTG-IADE-TEST",
            ["Lines[0].ItemName"] = "İade Kalemi",
            ["Lines[0].Quantity"] = "1",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "150",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/invoice/salesreturns", response.Headers.Location!.ToString());

        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/invoice/salesreturns"));
        Assert.Contains("IAD", list);                 // IAD serisinden numara
        Assert.Contains("180,00", list);              // 150 + %20 KDV
        Assert.Contains("Satış İade", list);

        // İade satış faturaları listesine sızmamalı
        var salesList = await client.GetStringAsync("/invoice/sales");
        Assert.DoesNotContain("IAD2026", salesList);
    }

    [Fact]
    public async Task Faturadan_IadeTaslagi_Acilir()
    {
        var client = await LoggedInClient();

        // Kaynak fatura oluştur, listeden id'sini bul
        var save = await client.PostFormAsync("/invoice/save", "/invoice/sales/edit?type=Gross", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesWholesale",
            ["Currency"] = "TL",
            ["ExchangeRate"] = "1",
            ["FirmId"] = "1",
            ["InvoiceDate"] = "2026-07-05",
            ["Status"] = "Open",
            ["InvoiceNumber"] = "ENTG-IADE-KAYNAK",
            ["Lines[0].ItemName"] = "Kaynak Kalem",
            ["Lines[0].Quantity"] = "2",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "100",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var list = await client.GetStringAsync("/invoice/sales?q=ENTG-IADE-KAYNAK");
        int invoiceId = int.Parse(Regex.Match(list, "/invoice/delete/(\\d+)").Groups[1].Value);

        // İade taslağı: tip SalesReturn, kaynak numarası açıklamada, kaydedilmemiş (Id=0)
        var draft = WebUtility.HtmlDecode(await client.GetStringAsync($"/invoice/return/{invoiceId}"));
        Assert.Contains("name=\"Type\" value=\"SalesReturn\"", draft.Replace("  ", " "));
        Assert.Contains("ENTG-IADE-KAYNAK numaralı faturanın iadesidir.", draft);
        Assert.Contains("Satış İade Faturası", draft);
    }
}
