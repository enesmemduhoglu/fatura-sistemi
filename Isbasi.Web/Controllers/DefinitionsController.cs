using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("definitions")]
public class DefinitionsController : Controller
{
    private readonly AppDbContext _db;

    public DefinitionsController(AppDbContext db) => _db = db;

    // ---- Müşteri & Tedarikçi ----

    [HttpGet("firm")]
    public async Task<IActionResult> Firm(string? q, string? role)
    {
        var query = _db.Firms.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => f.Name.Contains(q) || (f.TaxNumber != null && f.TaxNumber.Contains(q)));
        if (role == "customer") query = query.Where(f => f.IsCustomer);
        else if (role == "supplier") query = query.Where(f => f.IsSupplier);

        var firms = await query.OrderBy(f => f.Name).ToListAsync();

        // Firma bakiyesi: açık satış faturaları alacak, açık alış faturaları borç oluşturur.
        // SQLite decimal üzerinde SQL Sum yapamadığı için bellekte toplanır.
        var openInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Open
                && i.Type != InvoiceType.SalesOrder && i.Type != InvoiceType.PurchaseOrder)
            .Select(i => new { i.FirmId, i.Type, i.GrandTotal, i.ExchangeRate })
            .ToListAsync();
        var balances = openInvoices
            .GroupBy(i => i.FirmId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(i =>
                {
                    var tl = Math.Round(i.GrandTotal * i.ExchangeRate, 2);
                    return i.Type is InvoiceType.Purchase or InvoiceType.Expense ? -tl : tl;
                }));
        ViewBag.Balances = balances;
        ViewBag.Query = q;
        ViewBag.Role = role;
        return View(firms);
    }

    [HttpGet("firm/edit")]
    public async Task<IActionResult> FirmEdit(int? id)
    {
        var firm = id.HasValue ? await _db.Firms.FindAsync(id.Value) : new Firm();
        if (firm == null) return NotFound();
        return View(firm);
    }

    [HttpPost("firm/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirmEdit(Firm model)
    {
        if (!model.IsCustomer && !model.IsSupplier)
            ModelState.AddModelError("", "Firma müşteri, tedarikçi ya da her ikisi olarak işaretlenmelidir.");
        if (!ModelState.IsValid) return View(model);

        if (model.Id == 0) _db.Firms.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Firma kaydedildi.";
        return RedirectToAction(nameof(Firm));
    }

    [HttpPost("firm/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirmDelete(int id)
    {
        var firm = await _db.Firms.FindAsync(id);
        if (firm == null) return NotFound();

        bool hasInvoices = await _db.Invoices.AnyAsync(i => i.FirmId == id);
        bool hasCheques = await _db.Cheques.AnyAsync(c => c.FirmId == id);
        bool hasReceipts = await _db.FreelanceReceipts.AnyAsync(r => r.FirmId == id);
        if (hasInvoices || hasCheques || hasReceipts)
        {
            TempData["Error"] = hasInvoices
                ? "Bu firmaya ait faturalar olduğu için silinemez."
                : hasCheques
                    ? "Bu firmaya ait çekler olduğu için silinemez."
                    : "Bu firmaya ait serbest meslek makbuzları olduğu için silinemez.";
        }
        else
        {
            _db.Firms.Remove(firm);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Firma silindi.";
        }
        return RedirectToAction(nameof(Firm));
    }

    // Cari ekstre: fatura, tahsilat/ödeme, çek ve SMM hareketleri kronolojik dökümde.
    // Borç = firmanın bize borcunu artıran (satış, verilen SMM, tedarikçiye ödeme/verilen çek),
    // Alacak = azaltan (alış/gider, tahsilat, alınan çek, alınan SMM); bakiye = borç − alacak.
    [HttpGet("firm/statement")]
    public async Task<IActionResult> FirmStatement(int id, DateTime? start, DateTime? end)
    {
        var firm = await _db.Firms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (firm == null) return NotFound();

        var entries = new List<StatementRow>();

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.FirmId == id
                && i.Type != InvoiceType.SalesOrder && i.Type != InvoiceType.PurchaseOrder)
            .ToListAsync();
        foreach (var i in invoices)
        {
            bool debit = i.IsSales;
            // Ekstre TL bazlıdır; dövizli belgede orijinal tutar açıklamada gösterilir
            string? description = i.Currency == "TL"
                ? i.Description
                : $"{i.GrandTotal:N2} {i.CurrencySymbol} × {i.ExchangeRate:N4} {i.Description}".TrimEnd();
            entries.Add(new StatementRow
            {
                Date = i.InvoiceDate,
                DocNo = i.InvoiceNumber,
                DocType = i.Type switch
                {
                    InvoiceType.SalesWholesale => "Satış Faturası",
                    InvoiceType.SalesRetail => "Satış Faturası",
                    InvoiceType.Purchase => "Alış Faturası",
                    _ => "Gider Faturası"
                },
                Description = description,
                Debit = debit ? i.GrandTotalTry : 0,
                Credit = debit ? 0 : i.GrandTotalTry,
                Link = i.Type switch
                {
                    InvoiceType.Purchase => $"/invoice/purchase/edit?id={i.Id}",
                    InvoiceType.Expense => $"/invoice/purchaseservices/edit?id={i.Id}",
                    _ => $"/invoice/sales/edit?id={i.Id}"
                }
            });
        }

        var payments = await _db.Payments.AsNoTracking()
            .Include(p => p.Invoice)
            .Include(p => p.Safe).Include(p => p.BankAccount)
            .Where(p => p.Invoice!.FirmId == id)
            .ToListAsync();
        foreach (var p in payments)
        {
            bool incoming = p.Invoice!.IsSales;   // tahsilat: borcu azaltır (alacak)
            entries.Add(new StatementRow
            {
                Date = p.Date,
                DocNo = p.Invoice.InvoiceNumber,
                DocType = incoming ? "Tahsilat" : "Ödeme",
                Description = p.Description ?? (p.Safe?.Name ?? p.BankAccount?.Name),
                Debit = incoming ? 0 : p.Amount,
                Credit = incoming ? p.Amount : 0,
                Link = $"/payment/add?invoiceId={p.InvoiceId}"
            });
        }

        var cheques = await _db.Cheques.AsNoTracking()
            .Where(c => c.FirmId == id && c.Status != ChequeStatus.Bounced)
            .ToListAsync();
        foreach (var c in cheques)
        {
            bool received = c.Type == ChequeType.Received;   // alınan çek: borcu azaltır
            entries.Add(new StatementRow
            {
                Date = c.IssueDate,
                DocNo = c.ChequeNumber,
                DocType = received ? "Alınan Çek" : "Verilen Çek",
                Description = c.BankName == null ? c.Description : $"{c.BankName} — vade {c.DueDate:dd.MM.yyyy}",
                Debit = received ? 0 : c.Amount,
                Credit = received ? c.Amount : 0,
                Link = $"/cheque/edit?id={c.Id}"
            });
        }

        var receipts = await _db.FreelanceReceipts.AsNoTracking()
            .Where(r => r.FirmId == id)
            .ToListAsync();
        foreach (var r in receipts)
        {
            bool issued = r.Type == ReceiptType.Issued;   // verilen SMM: fatura gibi borçlandırır
            entries.Add(new StatementRow
            {
                Date = r.Date,
                DocNo = r.ReceiptNumber,
                DocType = issued ? "Verilen SMM" : "Alınan SMM",
                Description = r.Description,
                Debit = issued ? r.NetAmount : 0,
                Credit = issued ? 0 : r.NetAmount,
                Link = $"/receipt/edit?id={r.Id}"
            });
        }

        var ordered = entries.OrderBy(e => e.Date).ThenBy(e => e.DocNo).ToList();

        var vm = new FirmStatementViewModel { Firm = firm, Start = start, End = end };
        decimal balance = 0;
        foreach (var entry in ordered)
        {
            if (start.HasValue && entry.Date < start.Value)
            {
                balance += entry.Debit - entry.Credit;   // filtre öncesi hareketler devire toplanır
                continue;
            }
            if (end.HasValue && entry.Date >= end.Value.AddDays(1)) continue;

            if (start.HasValue && !vm.HasOpening)
            {
                vm.OpeningBalance = balance;
                vm.HasOpening = true;
            }
            balance += entry.Debit - entry.Credit;
            entry.Balance = balance;
            vm.Rows.Add(entry);
        }
        if (start.HasValue && !vm.HasOpening) { vm.OpeningBalance = balance; vm.HasOpening = true; }

        return View(vm);
    }

    // ---- Ürünler ----

    [HttpGet("product")]
    public async Task<IActionResult> Product(string? q)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || (p.Code != null && p.Code.Contains(q)));
        ViewBag.Query = q;
        ViewBag.Stocks = await StockCalculator.Compute(_db);
        return View(await query.OrderBy(p => p.Name).ToListAsync());
    }

    [HttpGet("product/edit")]
    public async Task<IActionResult> ProductEdit(int? id)
    {
        var product = id.HasValue ? await _db.Products.FindAsync(id.Value) : new Product();
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost("product/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductEdit(Product model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Id == 0) _db.Products.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Ürün kaydedildi.";
        return RedirectToAction(nameof(Product));
    }

    [HttpPost("product/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductDelete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        bool used = await _db.InvoiceLines.AnyAsync(l => l.ProductId == id);
        if (used)
        {
            TempData["Error"] = "Bu ürün faturalarda kullanıldığı için silinemez.";
        }
        else
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Ürün silindi.";
        }
        return RedirectToAction(nameof(Product));
    }

    // ---- Kasalar ----

    [HttpGet("safe")]
    public async Task<IActionResult> Safe()
    {
        ViewBag.Balances = await CashBalanceCalculator.Compute(_db);
        return View(await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync());
    }

    [HttpGet("safe/edit")]
    public async Task<IActionResult> SafeEdit(int? id)
    {
        var safe = id.HasValue ? await _db.Safes.FindAsync(id.Value) : new Safe();
        if (safe == null) return NotFound();
        return View(safe);
    }

    [HttpPost("safe/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SafeEdit(Safe model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Id == 0) _db.Safes.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Kasa kaydedildi.";
        return RedirectToAction(nameof(Safe));
    }

    [HttpPost("safe/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SafeDelete(int id)
    {
        var safe = await _db.Safes.FindAsync(id);
        if (safe == null) return NotFound();

        bool hasPayments = await _db.Payments.AnyAsync(p => p.SafeId == id)
            || await _db.Cheques.AnyAsync(c => c.SafeId == id);
        if (hasPayments)
        {
            TempData["Error"] = "Bu kasada tahsilat/ödeme ya da çek hareketi olduğu için silinemez.";
        }
        else
        {
            _db.Safes.Remove(safe);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Kasa silindi.";
        }
        return RedirectToAction(nameof(Safe));
    }

    // ---- Banka Hesapları ----

    [HttpGet("bank")]
    public async Task<IActionResult> Bank()
    {
        ViewBag.Balances = await CashBalanceCalculator.Compute(_db);
        return View(await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync());
    }

    [HttpGet("bank/edit")]
    public async Task<IActionResult> BankEdit(int? id)
    {
        var bank = id.HasValue ? await _db.BankAccounts.FindAsync(id.Value) : new BankAccount();
        if (bank == null) return NotFound();
        return View(bank);
    }

    [HttpPost("bank/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BankEdit(BankAccount model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Id == 0) _db.BankAccounts.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Banka hesabı kaydedildi.";
        return RedirectToAction(nameof(Bank));
    }

    [HttpPost("bank/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BankDelete(int id)
    {
        var bank = await _db.BankAccounts.FindAsync(id);
        if (bank == null) return NotFound();

        bool hasPayments = await _db.Payments.AnyAsync(p => p.BankAccountId == id)
            || await _db.Cheques.AnyAsync(c => c.BankAccountId == id);
        if (hasPayments)
        {
            TempData["Error"] = "Bu hesapta tahsilat/ödeme ya da çek hareketi olduğu için silinemez.";
        }
        else
        {
            _db.BankAccounts.Remove(bank);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Banka hesabı silindi.";
        }
        return RedirectToAction(nameof(Bank));
    }

    // ---- Hizmetler ----

    [HttpGet("service")]
    public async Task<IActionResult> Service(string? q)
    {
        var query = _db.Services.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.Name.Contains(q) || (s.Code != null && s.Code.Contains(q)));
        ViewBag.Query = q;
        return View(await query.OrderBy(s => s.Name).ToListAsync());
    }

    [HttpGet("service/edit")]
    public async Task<IActionResult> ServiceEdit(int? id)
    {
        var service = id.HasValue ? await _db.Services.FindAsync(id.Value) : new Service();
        if (service == null) return NotFound();
        return View(service);
    }

    [HttpPost("service/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ServiceEdit(Service model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Id == 0) _db.Services.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Hizmet kaydedildi.";
        return RedirectToAction(nameof(Service));
    }

    [HttpPost("service/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ServiceDelete(int id)
    {
        var service = await _db.Services.FindAsync(id);
        if (service == null) return NotFound();

        bool used = await _db.InvoiceLines.AnyAsync(l => l.ServiceId == id);
        if (used)
        {
            TempData["Error"] = "Bu hizmet faturalarda kullanıldığı için silinemez.";
        }
        else
        {
            _db.Services.Remove(service);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Hizmet silindi.";
        }
        return RedirectToAction(nameof(Service));
    }
}
