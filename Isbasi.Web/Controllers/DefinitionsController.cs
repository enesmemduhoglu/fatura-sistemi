using Isbasi.Web.Data;
using Isbasi.Web.Models;
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
            .Where(i => i.Status == InvoiceStatus.Open)
            .Select(i => new { i.FirmId, i.Type, i.GrandTotal })
            .ToListAsync();
        var balances = openInvoices
            .GroupBy(i => i.FirmId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(i => i.Type is InvoiceType.Purchase or InvoiceType.Expense ? -i.GrandTotal : i.GrandTotal));
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
        if (hasInvoices)
        {
            TempData["Error"] = "Bu firmaya ait faturalar olduğu için silinemez.";
        }
        else
        {
            _db.Firms.Remove(firm);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Firma silindi.";
        }
        return RedirectToAction(nameof(Firm));
    }

    // ---- Ürünler ----

    [HttpGet("product")]
    public async Task<IActionResult> Product(string? q)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || (p.Code != null && p.Code.Contains(q)));
        ViewBag.Query = q;
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
        => View(await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync());

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
        _db.Safes.Remove(safe);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Kasa silindi.";
        return RedirectToAction(nameof(Safe));
    }

    // ---- Banka Hesapları ----

    [HttpGet("bank")]
    public async Task<IActionResult> Bank()
        => View(await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync());

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
        _db.BankAccounts.Remove(bank);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Banka hesabı silindi.";
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
