---
name: verify
description: İşbaşı klonunu gerçek HTTP yüzeyinden doğrulama tarifi — uygulamayı ayağa kaldırma, curl ile form/login akışı sürme, bilinen tuzaklar.
---

# İşbaşı doğrulama tarifi

## Ayağa kaldırma

```powershell
& "C:\Program Files\dotnet\dotnet.exe" run --project "C:\Users\enesm\visual studio\isbasi\Isbasi.Web" --no-launch-profile --urls http://localhost:5210
```

- Arka planda çalıştır; `http://localhost:5210/account/login` 200 dönene dek bekle (~10 sn).
- Veritabanı: gerçek PostgreSQL (bağlantı kök dizindeki .env'den yüklenir — .env dosyasını okuma, uygulama kendisi yükler).
- İş bitince süreci durdur (TaskStop); LoginThrottle kilidi bellekte olduğundan yeniden başlatma kilitleri temizler.

## Form POST akışı (antiforgery zorunlu)

Tüm POST'lar antiforgery token ister (token'sız POST → 400). curl ile:

```bash
curl -s -c jar.txt http://localhost:5210/account/login -o page.html
TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' page.html | sed 's/.*value="//;s/"$//')
curl -s -b jar.txt -c jar.txt -X POST http://localhost:5210/account/login \
  --data-urlencode "email=..." --data-urlencode "password=..." \
  --data-urlencode "__RequestVerificationToken=$TOKEN"
```

- Başarılı giriş: 302 → `/`, `.AspNetCore.Cookies` çerezi jar'a düşer; sonraki istekler `-b jar.txt` ile yetkili.
- Aynı token + çerez seti birden çok POST'ta yeniden kullanılabilir.

## Tuzaklar

- Razor, Türkçe karakterleri HTML entity yazar (`Ç` → `&#xC7;`); yanıt gövdesinde Türkçe metin ararken entity biçimini de ara ya da decode et.
- Giriş denemeleri e-posta başına kilitlenir (5 hata / 15 dk). Prob için gerçek kullanıcı e-postasını değil sahte bir e-posta kullan; gerçek hesabı kilitleme.
- Gerçek DB'ye veri yazan akışları (fatura kaydet, ek yükle) canlıda sürme; entegrasyon testleri (SQLite in-memory, gerçek HTTP boru hattı) bu akışları kapsıyor.
