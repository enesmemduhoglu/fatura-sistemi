# Fatura Sistemi

[![CI](https://github.com/enesmemduhoglu/fatura-sistemi/actions/workflows/ci.yml/badge.svg)](https://github.com/enesmemduhoglu/fatura-sistemi/actions/workflows/ci.yml)

Küçük işletmeler için ön muhasebe ve faturalama web uygulaması. ASP.NET Core 8 (MVC) ve PostgreSQL ile geliştirilmiş kişisel bir projedir; Logo İşbaşı'ndan esinlenmiştir.

## Modüller

- **Satışlar** — satış faturaları, iade faturaları, siparişler, tekrarlayan faturalar, meslek makbuzu
- **Alışlar** — alış faturaları, iade faturaları, alış siparişleri, giderler, tedarikçiler
- **Stok** — ürünler, hizmetler, stok hareketleri
- **Para** — nakit durumu, ödemeler/tahsilatlar, kasalar, banka hesapları, banka mutabakatı, çekler
- **Raporlar** — satış/alış/masraf özetleri, KDV raporu, sipariş durumu
- Fatura PDF çıktısı (QuestPDF), faturaya dosya eki, firma yönetimi ve giriş sistemi

## Teknolojiler

- .NET 8, ASP.NET Core MVC
- PostgreSQL 17 + Entity Framework Core (Npgsql, EF Migrations)
- QuestPDF (fatura PDF'i), DotNetEnv (ortam değişkenleri)
- xUnit test paketi (105 test, SQLite in-memory üzerinde çalışır)

## Kurulum

Gereksinimler: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ve PostgreSQL 17.

1. Depoyu klonlayın.
2. Repo kökünde `.env` dosyası oluşturun — şablon olarak `.env.example` dosyasını kopyalayıp değerleri doldurun:

   ```
   DB_HOST=
   DB_PORT=
   DB_NAME=
   DB_USER=
   DB_PASSWORD=
   ```

   `.env` git'e girmez; gizli değerler asla depoya yazılmaz.

3. Uygulamayı başlatın:

   ```
   dotnet run --project Isbasi.Web
   ```

   Veritabanı şeması açılışta EF Migrations ile otomatik uygulanır; veritabanı boşsa örnek veri ve başlangıç kullanıcısı eklenir (bkz. `Isbasi.Web/Data/SeedData.cs`). Uygulama konsolda yazan adreste (varsayılan `http://localhost:5000`) açılır.

## Testler

```
dotnet test Isbasi.Tests
```

Testler in-memory SQLite kullanır; PostgreSQL kurulumu gerektirmez. Aynı paket her push'ta GitHub Actions üzerinde de çalışır.

## Veritabanı yedeği

`scripts/Backup-Database.ps1` bağlantı bilgilerini `.env`'den okur, `pg_dump` ile tarih damgalı yedek alır (`%USERPROFILE%\isbasi-yedekler`) ve en yeni 30 yedek dışındakileri siler.

Elle çalıştırma:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Backup-Database.ps1
```

Her gün 20:00'de otomatik yedek için (tek seferlik kayıt):

```powershell
schtasks /Create /SC DAILY /ST 20:00 /TN "IsbasiDbYedek" /TR "powershell -NoProfile -ExecutionPolicy Bypass -File \"C:\Users\enesm\visual studio\isbasi\scripts\Backup-Database.ps1\""
```

Geri yükleme örneği:

```powershell
pg_restore -h localhost -U postgres -d isbasi --clean --if-exists <yedek-dosyasi>
```

## Şema değişiklikleri

Yeni migration eklemek için:

```
dotnet ef migrations add <Ad> --project Isbasi.Web
```

Uygulama açılışta `Database.Migrate()` çağırdığı için migration'lar elle uygulanmaz.
