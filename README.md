# Fatura Sistemi

[![CI](https://github.com/enesmemduhoglu/fatura-sistemi/actions/workflows/ci.yml/badge.svg)](https://github.com/enesmemduhoglu/fatura-sistemi/actions/workflows/ci.yml)

Küçük işletmeler için ön muhasebe ve faturalama web uygulaması. ASP.NET Core 8 (MVC) ve PostgreSQL ile geliştirilmiş kişisel bir projedir; Logo İşbaşı'ndan esinlenmiştir.

## 🚀 Canlı Demo

**[https://fatura-sistemi.onrender.com](https://invoicesystem-sfgr.onrender.com)** — buradan canlı deneyebilirsiniz.

Demo giriş bilgileri:

| E-posta | Parola |
|---|---|
| `demo@yildizticaret.com` | `faturaSistemi1` |

> Ücretsiz sunucuda çalıştığı için 15 dakika boyunca ziyaret edilmezse uykuya geçer; ilk açılış 30-50 saniye sürebilir. Demo ortamıdır, girilen veriler kalıcı değildir.

## Modüller

- **Satışlar** — satış faturaları, iade faturaları, siparişler, tekrarlayan faturalar, meslek makbuzu
- **Alışlar** — alış faturaları, iade faturaları, alış siparişleri, giderler, tedarikçiler
- **Stok** — ürünler, hizmetler, stok hareketleri
- **Para** — nakit durumu, ödemeler/tahsilatlar, kasalar, banka hesapları, banka mutabakatı, çekler
- **Raporlar** — satış/alış/masraf özetleri, KDV raporu, sipariş durumu
- Fatura PDF çıktısı (QuestPDF), faturaya dosya eki, firma yönetimi ve giriş sistemi
- **Çöp kutusu** — silinen belgeler (fatura, tahsilat/ödeme, çek, makbuz) veritabanından kaldırılmaz; listelerden ve raporlardan düşer, tek tıkla geri alınır, kalıcı silme ikinci onay ister

## Güvenlik

- Parolalar PBKDF2 (SHA-256, 100.000 tur) ile saklanır; girişte sabit zamanlı karşılaştırma
- Kaba kuvvet koruması: aynı e-posta için 15 dakikada 5 hatalı denemeden sonra giriş kilitlenir
- Tüm POST istekleri antiforgery token ister (global otomatik doğrulama)
- Güvenlik başlıkları: `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, CSP `frame-ancestors 'none'`
- Oturum çerezi `HttpOnly` + `SameSite=Lax`; HTTPS altında `Secure`
- Dosya ekleri web kökü dışında GUID adla saklanır; içerik türü istemci beyanına değil uzantıya göre belirlenir
- CI her push'ta bağımlılıkları bilinen zafiyetlere karşı tarar (`dotnet list package --vulnerable`)
- Gizli değerler koda yazılmaz: bağlantı bilgileri ve ilk kullanıcı parolası (`SEED_USER_PASSWORD`) `.env`'den gelir

## Teknolojiler

- .NET 8, ASP.NET Core MVC
- PostgreSQL 17 + Entity Framework Core (Npgsql, EF Migrations)
- QuestPDF (fatura PDF'i), DotNetEnv (ortam değişkenleri)
- xUnit test paketi (125 test, SQLite in-memory üzerinde çalışır)

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
   SEED_USER_PASSWORD=   # ilk açılışta oluşturulacak kullanıcının parolası
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

## Veritabanı yedeği ve geri yükleme

`scripts/Backup-Database.ps1` bağlantı bilgilerini `.env`'den okur, `pg_dump` ile tarih damgalı yedek alır (`%USERPROFILE%\isbasi-yedekler`) ve **kademeli saklama** uygular — böylece hem yakın geçmişin hem de eski dönemlerin yedekleri elde kalır:

- son **14 günün** tüm yedekleri,
- son **8 haftada** her haftanın en yeni yedeği,
- son **12 ayda** her ayın en yeni yedeği tutulur; kalanlar silinir. (Süreler parametreyle değiştirilebilir.)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Backup-Database.ps1
```

Her gün 20:00'de otomatik yedek için Görev Zamanlayıcı'ya tek seferlik kayıt (repo yolunu kendi yolunuzla değiştirin):

```powershell
schtasks /Create /SC DAILY /ST 20:00 /TN "IsbasiDbYedek" /TR "powershell -NoProfile -ExecutionPolicy Bypass -File \"C:\repo-yolu\scripts\Backup-Database.ps1\""
```

> **İkinci kopya:** Yedekler uygulamayla aynı diskteyse disk arızasında ikisi birden kaybolur. Yedek klasörünü OneDrive/Google Drive gibi senkronize bir klasörün altına almak (`-BackupDir` parametresi) ya da düzenli olarak harici bir diske kopyalamak önerilir.

### Geri yükleme

`scripts/Restore-Database.ps1` üç şekilde kullanılır:

```powershell
# 1) Eldeki yedekleri gör
powershell ... -File scripts\Restore-Database.ps1 -List

# 2) Canlı veritabanını bir yedeğe döndür (önce otomatik güvenlik yedeği alınır;
#    uygulama kapalıyken çalıştırın)
powershell ... -File scripts\Restore-Database.ps1 -File isbasi_2026-07-05_2000.dump -Force

# 3) Yedeği canlıya dokunmadan ayrı bir veritabanına aç — silinen tek bir kaydı
#    bulup elle geri girmek için (pgAdmin/psql ile "isbasi_inceleme" içine bakın)
powershell ... -File scripts\Restore-Database.ps1 -File isbasi_2026-07-05_2000.dump -TargetDatabase isbasi_inceleme
```

Yanlışlıkla silmeye karşı ilk savunma zaten uygulamanın içindedir: belgeler çöp kutusuna gider ve tek tıkla geri alınır. Yedekten dönmek son çaredir.

## Uzaktan erişim (isteğe bağlı)

Uygulama tek kullanıcılık yerel kurulum için tasarlandı ve internete port açmadan da telefon/dizüstünden kullanılabilir. Önerilen yol [Tailscale](https://tailscale.com) (kişisel kullanım ücretsiz):

1. Uygulamanın çalıştığı bilgisayara ve telefona/dizüstüne Tailscale kurun, aynı hesapla giriş yapın.
2. Cihazlar kendi aralarında şifreli özel bir ağ kurar; telefondan `http://<pc-adi>:5000` (Docker'da `:8080`) ile uygulamaya erişilir.
3. Uygulama internete hiç açılmaz — dışarıdan tarama/bot trafiği ulaşamaz; uygulamanın kendi girişi ve deneme kilidi ikinci katman olarak kalır.

Tek şart, uygulamanın çalıştığı bilgisayarın açık olmasıdır (`docker compose up -d` bunu kolaylaştırır).

## Docker ile çalıştırma

Uygulamayı ve PostgreSQL'i birlikte ayağa kaldırmak için (repo kökünde `.env` dolu olmalı — compose `${...}` değerlerini oradan okur):

```
docker compose up -d
```

Uygulama `http://localhost:8080` adresinde açılır. Veritabanı verisi `pgdata`, fatura ekleri `appdata` volume'unda kalıcıdır. Durdurmak için `docker compose down` (volume'lar silinmez).

Gizli değerler imaja veya compose dosyasına yazılmaz: `.env` hem `.gitignore` hem `.dockerignore` içindedir; değerler yalnızca çalışma anında ortam değişkeni olarak konteynere geçer.

## Şema değişiklikleri

Yeni migration eklemek için:

```
dotnet ef migrations add <Ad> --project Isbasi.Web
```

Uygulama açılışta `Database.Migrate()` çağırdığı için migration'lar elle uygulanmaz.
