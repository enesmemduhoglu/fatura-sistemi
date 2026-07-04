using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>
/// Fatura eki yaşam döngüsü gerçek HTTP boru hattıyla: yükle → listede gör →
/// indir (aynı baytlar) → sil. Dosyalar factory'nin geçici köküne yazılır.
/// </summary>
public class AttachmentTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public AttachmentTests(IsbasiWebFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await client.LoginAsync();
        return client;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, int invoiceId,
        string fileName, byte[] bytes, string contentType = "application/pdf")
    {
        var token = await client.GetAntiforgeryToken($"/invoice/sales/edit?id={invoiceId}");
        var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" }
        };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return await client.PostAsync($"/invoice/attachments/upload/{invoiceId}", content);
    }

    [Fact]
    public async Task Ek_YukleIndirSil_TamDongu()
    {
        var client = await LoggedInClient();
        var bytes = Encoding.UTF8.GetBytes("ek testi içeriği — türkçe karakterli");

        var response = await Upload(client, 1, "sözleşme belgesi.pdf", bytes);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        // Düzenleme sayfasında listelenir (Razor, Türkçe karakterleri entity yapar; decode et)
        var edit = WebUtility.HtmlDecode(await client.GetStringAsync("/invoice/sales/edit?id=1"));
        Assert.Contains("sözleşme belgesi.pdf", edit);

        // İndirme orijinal baytları ve adı verir
        var match = Regex.Match(edit, "/invoice/attachments/download/(\\d+)");
        Assert.True(match.Success, "Düzenleme sayfasında indirme bağlantısı yok.");
        int attachmentId = int.Parse(match.Groups[1].Value);

        var download = await client.GetAsync($"/invoice/attachments/download/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Contains("pdf", download.Content.Headers.ContentType!.MediaType);

        // Silme hem kaydı hem listeyi temizler
        var delete = await client.PostFormAsync($"/invoice/attachments/delete/{attachmentId}",
            "/invoice/sales/edit?id=1", new());
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        var editAfter = WebUtility.HtmlDecode(await client.GetStringAsync("/invoice/sales/edit?id=1"));
        Assert.DoesNotContain("sözleşme belgesi.pdf", editAfter);

        var downloadAfter = await client.GetAsync($"/invoice/attachments/download/{attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, downloadAfter.StatusCode);
    }

    [Fact]
    public async Task IzinVerilmeyenUzanti_Reddedilir()
    {
        var client = await LoggedInClient();

        var response = await Upload(client, 1, "zararli.exe", new byte[] { 1, 2, 3 },
            "application/octet-stream");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);   // hata TempData ile döner

        var edit = await client.GetStringAsync("/invoice/sales/edit?id=1");
        Assert.DoesNotContain("zararli.exe", edit);
    }

    [Fact]
    public async Task BoyutSiniriAsan_Reddedilir()
    {
        var client = await LoggedInClient();

        var oversize = new byte[10 * 1024 * 1024 + 1];
        var response = await Upload(client, 1, "devasa.pdf", oversize);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var edit = await client.GetStringAsync("/invoice/sales/edit?id=1");
        Assert.DoesNotContain("devasa.pdf", edit);
    }

    [Fact]
    public async Task GirisYapilmadan_EkIndirilemez()
    {
        var anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await anonymous.GetAsync("/invoice/attachments/download/1");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task FaturaSilinince_EkDosyasiDiskten_Silinir()
    {
        var client = await LoggedInClient();

        // Ek işlemleri diğer testlerle çakışmasın diye kendi faturasını oluşturur
        var save = await client.PostFormAsync("/invoice/save", "/invoice/sales/edit?type=Gross", new()
        {
            ["Id"] = "0",
            ["Type"] = "SalesWholesale",
            ["Currency"] = "TL",
            ["ExchangeRate"] = "1",
            ["FirmId"] = "1",
            ["InvoiceDate"] = "2026-07-04",
            ["Status"] = "Open",
            ["InvoiceNumber"] = "ENTG-EK-1",
            ["Lines[0].ItemName"] = "Ekli Kalem",
            ["Lines[0].Quantity"] = "1",
            ["Lines[0].Unit"] = "Adet",
            ["Lines[0].UnitPrice"] = "100",
            ["Lines[0].VatRate"] = "20"
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        var list = await client.GetStringAsync("/invoice/sales?q=ENTG-EK-1");
        int invoiceId = int.Parse(Regex.Match(list, "/invoice/delete/(\\d+)").Groups[1].Value);

        int filesBefore = CountFiles();
        var upload = await Upload(client, invoiceId, "irsaliye.pdf", new byte[] { 9, 9, 9 });
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.Equal(filesBefore + 1, CountFiles());

        var delete = await client.PostFormAsync($"/invoice/delete/{invoiceId}",
            $"/invoice/sales/edit?id={invoiceId}", new());
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.Equal(filesBefore, CountFiles());   // fiziksel dosya da temizlendi
    }

    private int CountFiles() => Directory.Exists(_factory.AttachmentsRoot)
        ? Directory.GetFiles(_factory.AttachmentsRoot).Length
        : 0;
}
