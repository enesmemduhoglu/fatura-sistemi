using System.Globalization;
using System.Text;
using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("manage")]
public class ManageController : Controller
{
    private readonly AppDbContext _db;

    public ManageController(AppDbContext db) => _db = db;

    [HttpGet("index")]
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var settings = await _db.CompanySettings.FirstOrDefaultAsync() ?? new CompanySettings();
        return View(settings);
    }

    [HttpPost("index")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CompanySettings model)
    {
        if (!ModelState.IsValid) return View(model);

        if (model.Id == 0) _db.CompanySettings.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Firma bilgileri kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    // ---- Verileri Dışa Aktar ----

    [HttpGet("exportdata")]
    public IActionResult ExportData() => View();

    [HttpGet("exportdata/{dataset}")]
    public async Task<IActionResult> ExportDataset(string dataset)
    {
        var sb = new StringBuilder();
        string fileName;

        switch (dataset)
        {
            case "firms":
                sb.AppendLine("Firma Ünvanı;Tip;Müşteri;Tedarikçi;Adres;Ülke;İl;İlçe;Vergi Dairesi;Vergi No;E-Posta;Telefon");
                foreach (var f in await _db.Firms.AsNoTracking().OrderBy(f => f.Name).ToListAsync())
                    sb.AppendLine($"{Csv(f.Name)};{(f.Kind == FirmKind.Individual ? "Bireysel" : "Kurumsal")};{(f.IsCustomer ? "Evet" : "Hayır")};{(f.IsSupplier ? "Evet" : "Hayır")};{Csv(f.Address)};{Csv(f.Country)};{Csv(f.City)};{Csv(f.District)};{Csv(f.TaxOffice)};{Csv(f.TaxNumber)};{Csv(f.Email)};{Csv(f.Phone)}");
                fileName = "musteri-tedarikci";
                break;
            case "products":
                sb.AppendLine("Ürün Kodu;Ürün Adı;Birim;Satış Fiyatı;Alış Fiyatı;KDV %;Açılış Stok");
                foreach (var p in await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync())
                    sb.AppendLine($"{Csv(p.Code)};{Csv(p.Name)};{Csv(p.Unit)};{p.SalePrice:N2};{p.PurchasePrice:N2};{p.VatRate:0.##};{p.StockAmount:0.##}");
                fileName = "urunler";
                break;
            case "services":
                sb.AppendLine("Hizmet Kodu;Hizmet Adı;Birim;Birim Fiyat;KDV %");
                foreach (var s in await _db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync())
                    sb.AppendLine($"{Csv(s.Code)};{Csv(s.Name)};{Csv(s.Unit)};{s.Price:N2};{s.VatRate:0.##}");
                fileName = "hizmetler";
                break;
            case "invoices":
                sb.AppendLine("Belge No;Firma;Tip;Tarih;Vade;Matrah;KDV;Genel Toplam;Durum");
                foreach (var i in await _db.Invoices.AsNoTracking().Include(i => i.Firm).OrderBy(i => i.InvoiceDate).ToListAsync())
                {
                    string type = i.Type switch
                    {
                        InvoiceType.SalesWholesale => "Toptan Satış",
                        InvoiceType.SalesRetail => "Perakende Satış",
                        InvoiceType.Purchase => "Alış",
                        InvoiceType.Expense => "Gider",
                        InvoiceType.SalesOrder => "Satış Siparişi",
                        _ => "Alış Siparişi"
                    };
                    string status = i.IsOrder
                        ? i.OrderState switch { OrderStatus.Invoiced => "Faturalandı", OrderStatus.Cancelled => "İptal", _ => "Bekliyor" }
                        : (i.Status == InvoiceStatus.Paid ? "Ödendi" : "Açık");
                    sb.AppendLine($"{Csv(i.InvoiceNumber)};{Csv(i.Firm?.Name)};{type};{i.InvoiceDate:dd.MM.yyyy};{i.DueDate:dd.MM.yyyy};{i.GrandTotal - i.VatTotal:N2};{i.VatTotal:N2};{i.GrandTotal:N2};{status}");
                }
                fileName = "faturalar";
                break;
            case "payments":
                sb.AppendLine("Tarih;Belge No;Firma;Tutar;Hesap;Açıklama");
                foreach (var p in await _db.Payments.AsNoTracking()
                    .Include(p => p.Invoice).ThenInclude(i => i!.Firm)
                    .Include(p => p.Safe).Include(p => p.BankAccount)
                    .OrderBy(p => p.Date).ToListAsync())
                    sb.AppendLine($"{p.Date:dd.MM.yyyy};{Csv(p.Invoice?.InvoiceNumber)};{Csv(p.Invoice?.Firm?.Name)};{p.Amount:N2};{Csv(p.Safe?.Name ?? p.BankAccount?.Name)};{Csv(p.Description)}");
                fileName = "tahsilat-odeme";
                break;
            case "cheques":
                sb.AppendLine("Çek No;Tip;Firma;Banka;Düzenleme;Vade;Tutar;Durum");
                foreach (var c in await _db.Cheques.AsNoTracking().Include(c => c.Firm).OrderBy(c => c.DueDate).ToListAsync())
                    sb.AppendLine($"{Csv(c.ChequeNumber)};{(c.Type == ChequeType.Received ? "Alınan" : "Verilen")};{Csv(c.Firm?.Name)};{Csv(c.BankName)};{c.IssueDate:dd.MM.yyyy};{c.DueDate:dd.MM.yyyy};{c.Amount:N2};{c.Status switch { ChequeStatus.Cleared => "Tahsil Edildi", ChequeStatus.Bounced => "Karşılıksız", _ => "Portföyde" }}");
                fileName = "cekler";
                break;
            case "receipts":
                sb.AppendLine("Makbuz No;Tip;Firma;Tarih;Brüt;Stopaj;KDV;Net;Durum");
                foreach (var r in await _db.FreelanceReceipts.AsNoTracking().Include(r => r.Firm).OrderBy(r => r.Date).ToListAsync())
                    sb.AppendLine($"{Csv(r.ReceiptNumber)};{(r.Type == ReceiptType.Issued ? "Verilen" : "Alınan")};{Csv(r.Firm?.Name)};{r.Date:dd.MM.yyyy};{r.GrossAmount:N2};{r.StopajAmount:N2};{r.VatAmount:N2};{r.NetAmount:N2};{(r.Status == InvoiceStatus.Paid ? "Ödendi" : "Açık")}");
                fileName = "serbest-meslek-makbuzlari";
                break;
            default:
                return NotFound();
        }

        return CsvFile(sb, $"{fileName}-{DateTime.Today:yyyyMMdd}.csv");
    }

    // ---- Verileri İçe Aktar ----

    [HttpGet("importdata")]
    public IActionResult ImportData() => View();

    [HttpGet("importtemplate/{dataset}")]
    public IActionResult ImportTemplate(string dataset)
    {
        var sb = new StringBuilder();
        switch (dataset)
        {
            case "firms":
                sb.AppendLine("Firma Ünvanı;Tip;Müşteri;Tedarikçi;Adres;Ülke;İl;İlçe;Vergi Dairesi;Vergi No;E-Posta;Telefon");
                sb.AppendLine("Örnek Ticaret A.Ş.;Kurumsal;Evet;Hayır;Örnek Cad. No:1;Türkiye;İstanbul;Kadıköy;Kadıköy;1112223334;ornek@firma.com;0216 111 22 33");
                return CsvFile(sb, "musteri-tedarikci-sablon.csv");
            case "products":
                sb.AppendLine("Ürün Kodu;Ürün Adı;Birim;Satış Fiyatı;Alış Fiyatı;KDV %;Açılış Stok");
                sb.AppendLine("URN-100;Örnek Ürün;Adet;150,00;100,00;20;50");
                return CsvFile(sb, "urun-sablon.csv");
            default:
                return NotFound();
        }
    }

    [HttpPost("importdata")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportData(string dataset, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Lütfen bir CSV dosyası seçin.";
            return RedirectToAction(nameof(ImportData));
        }

        List<string[]> rows;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            var content = await reader.ReadToEndAsync();
            rows = content.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(';'))
                .ToList();
        }

        // Başlık satırı varsa atla
        if (rows.Count > 0 && (rows[0][0].Contains("Ünvanı") || rows[0][0].Contains("Kodu") || rows[0][0].Contains("Adı")))
            rows.RemoveAt(0);

        int added = 0, skipped = 0;
        var errors = new List<string>();

        if (dataset == "firms")
        {
            var existingNames = (await _db.Firms.Select(f => f.Name).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var cells in rows)
            {
                string name = Cell(cells, 0);
                if (name == "") { skipped++; continue; }
                if (!existingNames.Add(name)) { skipped++; continue; }

                _db.Firms.Add(new Firm
                {
                    Name = name,
                    Kind = Cell(cells, 1).Equals("Bireysel", StringComparison.OrdinalIgnoreCase) ? FirmKind.Individual : FirmKind.Corporate,
                    IsCustomer = !Cell(cells, 2).Equals("Hayır", StringComparison.OrdinalIgnoreCase),
                    IsSupplier = Cell(cells, 3).Equals("Evet", StringComparison.OrdinalIgnoreCase),
                    Address = NullIfEmpty(Cell(cells, 4)),
                    Country = NullIfEmpty(Cell(cells, 5)) ?? "Türkiye",
                    City = NullIfEmpty(Cell(cells, 6)),
                    District = NullIfEmpty(Cell(cells, 7)),
                    TaxOffice = NullIfEmpty(Cell(cells, 8)),
                    TaxNumber = NullIfEmpty(Cell(cells, 9)),
                    Email = NullIfEmpty(Cell(cells, 10)),
                    Phone = NullIfEmpty(Cell(cells, 11))
                });
                added++;
            }
        }
        else if (dataset == "products")
        {
            var existingKeys = (await _db.Products.Select(p => p.Code ?? p.Name).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var cells in rows)
            {
                string code = Cell(cells, 0);
                string name = Cell(cells, 1);
                if (name == "") { skipped++; continue; }
                if (!existingKeys.Add(code != "" ? code : name)) { skipped++; continue; }

                _db.Products.Add(new Product
                {
                    Code = NullIfEmpty(code),
                    Name = name,
                    Unit = NullIfEmpty(Cell(cells, 2)) ?? "Adet",
                    SalePrice = ParseDecimal(Cell(cells, 3)),
                    PurchasePrice = ParseDecimal(Cell(cells, 4)),
                    VatRate = ParseDecimal(Cell(cells, 5), 20),
                    StockAmount = ParseDecimal(Cell(cells, 6))
                });
                added++;
            }
        }
        else
        {
            TempData["Error"] = "Geçersiz veri türü.";
            return RedirectToAction(nameof(ImportData));
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"{added} kayıt içe aktarıldı" + (skipped > 0 ? $", {skipped} satır atlandı (boş ya da mevcut)." : ".");
        return RedirectToAction(nameof(ImportData));
    }

    // ---- Yardımcılar ----

    private static string Csv(string? value) => (value ?? "").Replace(";", ",");
    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index].Trim() : "";
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // Türkçe ("1.234,56") ve nokta ondalıklı ("1234.56") biçimlerin ikisini de kabul eder.
    // Virgül içeren değerler Türkçe sayılır; yalnızca nokta içerenlerde nokta ondalık
    // ayracıdır ("85.25" = 85,25 — tr kültürüyle 8525 olurdu).
    private static decimal ParseDecimal(string value, decimal fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var culture = value.Contains(',') ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
        return decimal.TryParse(value, NumberStyles.Number, culture, out var result) ? result : fallback;
    }

    private FileContentResult CsvFile(StringBuilder sb, string fileName)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", fileName);
    }
}
