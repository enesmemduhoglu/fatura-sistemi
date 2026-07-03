using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("payment")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;

    public PaymentController(AppDbContext db) => _db = db;

    // Tüm tahsilat/ödeme hareketleri tek listede; yön faturanın tipinden gelir
    [HttpGet("list")]
    public async Task<IActionResult> List(string? direction, string? account, string? q,
        DateTime? start, DateTime? end)
    {
        var query = _db.Payments.AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i!.Firm)
            .Include(p => p.Safe).Include(p => p.BankAccount)
            .AsQueryable();

        if (direction == "in")
            query = query.Where(p => p.Invoice!.Type == InvoiceType.SalesWholesale || p.Invoice.Type == InvoiceType.SalesRetail);
        else if (direction == "out")
            query = query.Where(p => p.Invoice!.Type == InvoiceType.Purchase || p.Invoice.Type == InvoiceType.Expense);

        if (account == "safe") query = query.Where(p => p.SafeId != null);
        else if (account == "bank") query = query.Where(p => p.BankAccountId != null);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Invoice!.Firm!.Name.Contains(q) || p.Invoice.InvoiceNumber.Contains(q));

        if (start.HasValue) query = query.Where(p => p.Date >= start.Value);
        if (end.HasValue) query = query.Where(p => p.Date < end.Value.AddDays(1));

        ViewBag.Direction = direction;
        ViewBag.Account = account;
        ViewBag.Query = q;
        ViewBag.Start = start;
        ViewBag.End = end;
        return View(await query.OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).ToListAsync());
    }

    [HttpGet("add")]
    public async Task<IActionResult> Add(int invoiceId)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .Include(i => i.Payments).ThenInclude(p => p.Safe)
            .Include(i => i.Payments).ThenInclude(p => p.BankAccount)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        if (invoice.IsOrder)
        {
            TempData["Error"] = "Siparişlere tahsilat/ödeme girilemez; önce faturaya dönüştürün.";
            return Redirect(invoice.Type == InvoiceType.PurchaseOrder ? "/invoice/purchaseorders" : "/invoice/orders");
        }

        await FillViewBags(invoice);
        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = Math.Max(invoice.RemainingTotal, 0)
        };
        return View(payment);
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Payment model)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);
        if (invoice == null) return NotFound();

        if (model.AccountType == PaymentAccountType.Safe)
        {
            model.BankAccountId = null;
            if (!model.SafeId.HasValue) ModelState.AddModelError("SafeId", "Kasa seçin.");
        }
        else
        {
            model.SafeId = null;
            if (!model.BankAccountId.HasValue) ModelState.AddModelError("BankAccountId", "Banka hesabı seçin.");
        }

        if (!ModelState.IsValid)
        {
            var fullInvoice = await _db.Invoices.AsNoTracking()
                .Include(i => i.Firm)
                .Include(i => i.Payments).ThenInclude(p => p.Safe)
                .Include(i => i.Payments).ThenInclude(p => p.BankAccount)
                .FirstAsync(i => i.Id == model.InvoiceId);
            await FillViewBags(fullInvoice);
            return View(model);
        }

        // Not: invoice izlendiği için EF, Add sırasında ilişki fixup'ı ile modeli
        // invoice.Payments'a kendisi ekler; elle de eklemek tutarı çift saydırır
        _db.Payments.Add(model);
        UpdateInvoiceStatus(invoice);
        await _db.SaveChangesAsync();

        TempData["Success"] = invoice.IsSales ? "Tahsilat kaydedildi." : "Ödeme kaydedildi.";
        return RedirectToAction(nameof(Add), new { invoiceId = model.InvoiceId });
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment == null) return NotFound();

        int invoiceId = payment.InvoiceId;
        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync();

        var invoice = await _db.Invoices.Include(i => i.Payments).FirstAsync(i => i.Id == invoiceId);
        UpdateInvoiceStatus(invoice);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Kayıt silindi.";
        return RedirectToAction(nameof(Add), new { invoiceId });
    }

    // Karşılaştırma TL bazlıdır: dövizli faturada kalan, TL karşılığı üzerinden izlenir
    private static void UpdateInvoiceStatus(Invoice invoice)
        => invoice.Status = invoice.PaidTotal >= invoice.GrandTotalTry && invoice.GrandTotalTry > 0
            ? InvoiceStatus.Paid
            : InvoiceStatus.Open;

    private async Task FillViewBags(Invoice invoice)
    {
        ViewBag.Invoice = invoice;
        ViewBag.Safes = await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.Banks = await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
    }
}
