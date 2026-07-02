using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("invoice")]
public class InvoiceController : Controller
{
    private readonly AppDbContext _db;

    public InvoiceController(AppDbContext db) => _db = db;

    [HttpGet("sales")]
    public Task<IActionResult> Sales(string? status, string? q, DateTime? start, DateTime? end)
        => List("Sales", status, q, start, end);

    [HttpGet("purchase")]
    public Task<IActionResult> Purchase(string? status, string? q, DateTime? start, DateTime? end)
        => List("Purchase", status, q, start, end);

    [HttpGet("purchaseservices")]
    public Task<IActionResult> Expenses(string? status, string? q, DateTime? start, DateTime? end)
        => List("Expense", status, q, start, end);

    private async Task<IActionResult> List(string mode, string? status, string? q, DateTime? start, DateTime? end)
    {
        var query = _db.Invoices.AsNoTracking().Include(i => i.Firm).Include(i => i.Payments).AsQueryable();
        query = mode switch
        {
            "Sales" => query.Where(i => i.Type == InvoiceType.SalesWholesale || i.Type == InvoiceType.SalesRetail),
            "Purchase" => query.Where(i => i.Type == InvoiceType.Purchase),
            _ => query.Where(i => i.Type == InvoiceType.Expense)
        };

        if (status == "Open") query = query.Where(i => i.Status == InvoiceStatus.Open);
        else if (status == "Paid") query = query.Where(i => i.Status == InvoiceStatus.Paid);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Firm!.Name.Contains(q) || i.InvoiceNumber.Contains(q));

        if (start.HasValue) query = query.Where(i => i.InvoiceDate >= start.Value);
        if (end.HasValue) query = query.Where(i => i.InvoiceDate < end.Value.AddDays(1));

        var vm = new InvoiceListViewModel
        {
            Mode = mode,
            Status = status,
            Query = q,
            StartDate = start,
            EndDate = end,
            Invoices = await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync()
        };
        return View("List", vm);
    }

    // type: Gross = Toptan (KDV Hariç), Net = Perakende (KDV Dahil)
    [HttpGet("sales/edit")]
    public Task<IActionResult> EditSales(int? id, string type = "Gross")
        => Edit(id, type == "Net" ? InvoiceType.SalesRetail : InvoiceType.SalesWholesale);

    [HttpGet("purchase/edit")]
    public Task<IActionResult> EditPurchase(int? id)
        => Edit(id, InvoiceType.Purchase);

    [HttpGet("purchaseservices/edit")]
    public Task<IActionResult> EditExpense(int? id)
        => Edit(id, InvoiceType.Expense);

    private async Task<IActionResult> Edit(int? id, InvoiceType type)
    {
        Invoice invoice;
        if (id.HasValue)
        {
            var existing = await _db.Invoices.AsNoTracking()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id.Value);
            if (existing == null) return NotFound();
            invoice = existing;
        }
        else
        {
            invoice = new Invoice { Type = type, InvoiceDate = DateTime.Now };
        }

        await FillEditViewBags(invoice);
        return View("Edit", invoice);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Invoice model, string? submitAction)
    {
        model.Lines = model.Lines.Where(l => !string.IsNullOrWhiteSpace(l.ItemName)).ToList();

        if (model.FirmId == 0)
            ModelState.AddModelError("FirmId", "Firma seçimi zorunludur.");
        if (model.Lines.Count == 0)
            ModelState.AddModelError("Lines", "En az bir fatura satırı ekleyin.");

        if (!ModelState.IsValid)
        {
            await FillEditViewBags(model);
            return View("Edit", model);
        }

        InvoiceCalculator.Calculate(model);

        if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            model.InvoiceNumber = await NextInvoiceNumber();

        if (model.Id == 0)
        {
            _db.Invoices.Add(model);
        }
        else
        {
            var existingLineIds = model.Lines.Where(l => l.Id != 0).Select(l => l.Id).ToList();
            var removed = _db.InvoiceLines.Where(l => l.InvoiceId == model.Id && !existingLineIds.Contains(l.Id));
            _db.InvoiceLines.RemoveRange(removed);

            _db.Update(model);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Fatura kaydedildi.";

        if (submitAction == "saveAndNew")
        {
            return model.Type switch
            {
                InvoiceType.Purchase => RedirectToAction(nameof(EditPurchase)),
                InvoiceType.Expense => RedirectToAction(nameof(EditExpense)),
                _ => RedirectToAction(nameof(EditSales), new { type = model.Type == InvoiceType.SalesRetail ? "Net" : "Gross" })
            };
        }
        return RedirectToList(model.Type);
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();

        var type = invoice.Type;
        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Fatura silindi.";
        return RedirectToList(type);
    }

    private IActionResult RedirectToList(InvoiceType type) => type switch
    {
        InvoiceType.Purchase => RedirectToAction(nameof(Purchase)),
        InvoiceType.Expense => RedirectToAction(nameof(Expenses)),
        _ => RedirectToAction(nameof(Sales))
    };

    private async Task<string> NextInvoiceNumber()
    {
        var year = DateTime.Today.Year;
        var prefix = $"ISB{year}";
        var last = await _db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out int lastSeq))
            seq = lastSeq + 1;
        return $"{prefix}{seq:D9}";
    }

    private async Task FillEditViewBags(Invoice invoice)
    {
        bool isSales = invoice.IsSales;
        bool isExpense = invoice.Type == InvoiceType.Expense;

        ViewBag.Firms = await _db.Firms.AsNoTracking()
            .Where(f => isSales ? f.IsCustomer : f.IsSupplier)
            .OrderBy(f => f.Name)
            .ToListAsync();

        // select2 + otomatik doldurma için ürün/hizmet kataloğu (giderlerde sadece hizmet/masraf)
        var services = await _db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        var items = services.Select(s => new
        {
            key = $"s{s.Id}",
            productId = (int?)null,
            serviceId = (int?)s.Id,
            name = s.Name,
            unit = s.Unit,
            price = s.Price,
            vatRate = s.VatRate
        }).ToList();

        if (!isExpense)
        {
            var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
            items = products.Select(p => new
            {
                key = $"p{p.Id}",
                productId = (int?)p.Id,
                serviceId = (int?)null,
                name = p.Name,
                unit = p.Unit,
                price = isSales ? p.SalePrice : p.PurchasePrice,
                vatRate = p.VatRate
            }).Concat(items).ToList();
        }
        ViewBag.Items = items;
    }
}
