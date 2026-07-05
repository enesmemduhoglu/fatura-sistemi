using System.Globalization;
using Isbasi.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

// Uygulama her yerde yerel DateTime.Now kullanır; Npgsql'in UTC zorunluluğu yerine
// eski davranış (timestamp without time zone) korunur
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Veritabanı bilgileri .env dosyasından gelir (çalışma dizininden yukarı doğru aranır;
// dosya yoksa sessiz geçer — testler ve ortam değişkeniyle çalışan ortamlar için)
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Tüm sayfalar giriş ister; Account controller'ı [AllowAnonymous] ile açılır
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AuthorizeFilter()));

// DB_* değişkenleri (.env ya da ortam) öncelikli; yoksa appsettings ConnectionStrings:Default
string? connectionString = builder.Configuration.GetConnectionString("Default");
string? dbHost = Environment.GetEnvironmentVariable("DB_HOST");
if (!string.IsNullOrWhiteSpace(dbHost))
{
    connectionString =
        $"Host={dbHost};" +
        $"Port={Environment.GetEnvironmentVariable("DB_PORT") ?? "5432"};" +
        $"Database={Environment.GetEnvironmentVariable("DB_NAME") ?? "isbasi"};" +
        $"Username={Environment.GetEnvironmentVariable("DB_USER") ?? "postgres"};" +
        $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")}";
}
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<AttachmentStorage>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Tutar/tarih biçimleri Türkçe görünsün (1.234,56 ve gg.aa.yyyy)
var trCulture = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(trCulture),
    SupportedCultures = new[] { trCulture },
    SupportedUICultures = new[] { trCulture }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // PostgreSQL'de şema migration'larla yönetilir; SQLite yalnızca testlerde kullanılır
    // ve migration'lar Npgsql'e özgü olduğundan orada EnsureCreated yeterlidir
    if (db.Database.IsNpgsql()) db.Database.Migrate();
    else db.Database.EnsureCreated();
    SeedData.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// WebApplicationFactory tabanlı entegrasyon testlerinin erişebilmesi için
public partial class Program { }
