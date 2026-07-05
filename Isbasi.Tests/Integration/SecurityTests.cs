using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Isbasi.Tests.Integration;

/// <summary>
/// Güvenlik sıkılaştırmaları gerçek HTTP boru hattıyla: yanıt başlıkları,
/// kaba kuvvet kilidi ve token'sız POST'un reddi.
/// </summary>
public class SecurityTests : IClassFixture<IsbasiWebFactory>
{
    private readonly IsbasiWebFactory _factory;

    public SecurityTests(IsbasiWebFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task TumYanitlarda_GuvenlikBasliklariVardir()
    {
        var response = await Client().GetAsync("/account/login");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task AntiforgeryTokensizPost_Reddedilir()
    {
        var client = Client();
        await client.LoginAsync();

        var response = await client.PostAsync("/account/logout", new FormUrlEncodedContent(
            new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BesHataliDenemedenSonra_DogruParolaBileGirisYapamaz()
    {
        // Diğer testlerin oturumlarını kilitlememek için kendine ait bir uygulama örneği kurar
        await using var factory = new IsbasiWebFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        for (int i = 0; i < 5; i++)
        {
            var attempt = await client.PostFormAsync("/account/login", "/account/login", new()
            {
                ["email"] = IsbasiWebFactory.SeedEmail,
                ["password"] = "yanlis-parola"
            });
            Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);   // hata mesajıyla sayfa döner
        }

        var blocked = await client.PostFormAsync("/account/login", "/account/login", new()
        {
            ["email"] = IsbasiWebFactory.SeedEmail,
            ["password"] = IsbasiWebFactory.SeedPassword   // doğru parola dahi kabul edilmez
        });

        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        // Razor, Türkçe karakterleri HTML entity olarak yazar; karşılaştırmadan önce çözülür
        Assert.Contains("Çok fazla hatalı deneme",
            WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("kisa1a7", "en az 8 karakter")]          // 7 karakter
    [InlineData("harflerrr", "bir harf ve bir rakam")]   // rakam yok
    [InlineData("12345678", "bir harf ve bir rakam")]    // harf yok
    public async Task ZayifYeniParola_Reddedilir(string newPassword, string expectedError)
    {
        var client = Client();
        await client.LoginAsync();

        var response = await client.PostFormAsync("/account/changepassword", "/account/changepassword", new()
        {
            ["currentPassword"] = IsbasiWebFactory.SeedPassword,
            ["newPassword"] = newPassword,
            ["newPasswordConfirm"] = newPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // hata mesajıyla form yeniden gösterilir
        Assert.Contains(expectedError,
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task KilitBaskaEpostayi_Etkilemez()
    {
        await using var factory = new IsbasiWebFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        for (int i = 0; i < 5; i++)
        {
            await client.PostFormAsync("/account/login", "/account/login", new()
            {
                ["email"] = "olmayan@ornek.com",
                ["password"] = "deneme"
            });
        }

        // Seed kullanıcısı etkilenmez, normal giriş yapabilir
        await client.LoginAsync();
    }
}
