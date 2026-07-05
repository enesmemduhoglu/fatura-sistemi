using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>/invoice/pdf/{id}: gerçek boru hattından PDF indirme.</summary>
public class PdfDownloadTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public PdfDownloadTests(IsbasiWebFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task SeedFatura_PdfOlarakIndirilir()
    {
        var client = Client();
        await client.LoginAsync();

        var response = await client.GetAsync("/invoice/pdf/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".pdf", response.Content.Headers.ContentDisposition!.FileNameStar
            ?? response.Content.Headers.ContentDisposition.FileName!);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task OlmayanFatura_404Doner()
    {
        var client = Client();
        await client.LoginAsync();

        var response = await client.GetAsync("/invoice/pdf/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GirisYapilmadan_PdfIndirilemez()
    {
        var response = await Client().GetAsync("/invoice/pdf/1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString());
    }
}
