using System.Globalization;
using Isbasi.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

// Uygulama her yerde yerel DateTime.Now kullanır; Npgsql'in UTC zorunluluğu yerine
// eski davranış (timestamp without time zone) korunur
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Tüm sayfalar giriş ister; Account controller'ı [AllowAnonymous] ile açılır
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AuthorizeFilter()));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
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
