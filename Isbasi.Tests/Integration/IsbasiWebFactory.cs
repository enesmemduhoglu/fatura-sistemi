using System.Net;
using System.Text.RegularExpressions;
using Isbasi.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isbasi.Tests.Integration;

/// <summary>
/// Uygulamayı gerçek HTTP boru hattıyla (routing, model binding, tag helper'lar,
/// antiforgery, tr-TR localization) in-memory SQLite üzerinde ayağa kaldırır.
/// Program.cs'teki EnsureCreated + SeedData aynen çalışır; testler seed veriye güvenebilir.
/// </summary>
public sealed class IsbasiWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Ek dosyaları gerçek App_Data yerine teste özel geçici klasöre yazılır.</summary>
    public string AttachmentsRoot { get; } =
        Path.Combine(Path.GetTempPath(), "isbasi-test-attachments-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Development");
        builder.UseSetting("Attachments:Root", AttachmentsRoot);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
        if (Directory.Exists(AttachmentsRoot))
            Directory.Delete(AttachmentsRoot, recursive: true);
    }
}

public static class HttpClientExtensions
{
    private static readonly Regex TokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    public static async Task<string> GetAntiforgeryToken(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, $"{url} sayfasında antiforgery token bulunamadı.");
        return match.Groups[1].Value;
    }

    /// <summary>Seed kullanıcısıyla form login yapar; çerez HttpClient'ta kalır.</summary>
    public static async Task LoginAsync(this HttpClient client)
    {
        var token = await client.GetAntiforgeryToken("/account/login");
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "eneshan034@gmail.com",
            ["password"] = "enes123",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client,
        string url, string tokenSourceUrl, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await client.GetAntiforgeryToken(tokenSourceUrl);
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }
}
