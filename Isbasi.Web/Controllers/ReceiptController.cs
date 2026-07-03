using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("receipt")]
public class ReceiptController : Controller
{
    private readonly AppDbContext _db;

    public ReceiptController(AppDbContext db) => _db = db;

    // kind: issued | received (FreelanceReceipt.Type ile isim çakışıp ModelState'e sızmasın diye "type" değil)
    [HttpGet("")]
    public async Task<IActionResult> Index(string kind = "issued", string? status = null, string? q = null)
    {
        var type = kind == "received" ? ReceiptType.Received : ReceiptType.Issued;
        var query = _db.FreelanceReceipts.AsNoTracking()
            .Include(r => r.Firm)
            .Where(r => r.Type == type);

        if (status == "Open") query = query.Where(r => r.Status == InvoiceStatus.Open);
        else if (status == "Paid") query = query.Where(r => r.Status == InvoiceStatus.Paid);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r => r.Firm!.Name.Contains(q) || r.ReceiptNumber.Contains(q));

        ViewBag.Kind = kind == "received" ? "received" : "issued";
        ViewBag.Status = status;
        ViewBag.Query = q;
        return View(await query.OrderByDescending(r => r.Date).ToListAsync());
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(int? id, string kind = "issued")
    {
        FreelanceReceipt receipt;
        if (id.HasValue)
        {
            var existing = await _db.FreelanceReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id.Value);
            if (existing == null) return NotFound();
            receipt = existing;
        }
        else
        {
            receipt = new FreelanceReceipt { Type = kind == "received" ? ReceiptType.Received : ReceiptType.Issued };
        }

        await FillViewBags(receipt);
        return View(receipt);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(FreelanceReceipt model)
    {
        if (model.FirmId == 0)
            ModelState.AddModelError("FirmId", "Firma seçimi zorunludur.");

        if (!ModelState.IsValid)
        {
            await FillViewBags(model);
            return View("Edit", model);
        }

        if (string.IsNullOrWhiteSpace(model.ReceiptNumber))
            model.ReceiptNumber = await NextReceiptNumber();

        if (model.Id == 0) _db.FreelanceReceipts.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Makbuz kaydedildi.";
        return RedirectToAction(nameof(Index), new { kind = model.Type == ReceiptType.Received ? "received" : "issued" });
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var receipt = await _db.FreelanceReceipts.FindAsync(id);
        if (receipt == null) return NotFound();

        _db.FreelanceReceipts.Remove(receipt);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Makbuz silindi.";
        return RedirectToAction(nameof(Index), new { kind = receipt.Type == ReceiptType.Received ? "received" : "issued" });
    }

    [HttpGet("print/{id:int}")]
    public async Task<IActionResult> Print(int id)
    {
        var receipt = await _db.FreelanceReceipts.AsNoTracking()
            .Include(r => r.Firm)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return NotFound();

        ViewBag.Company = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        return View(receipt);
    }

    private async Task<string> NextReceiptNumber()
    {
        var year = DateTime.Today.Year;
        var prefix = $"SMM{year}";
        var last = await _db.FreelanceReceipts
            .Where(r => r.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReceiptNumber)
            .Select(r => r.ReceiptNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out int lastNo))
            next = lastNo + 1;
        return $"{prefix}{next:D9}";
    }

    private async Task FillViewBags(FreelanceReceipt receipt)
    {
        bool isIssued = receipt.Type == ReceiptType.Issued;
        ViewBag.Firms = await _db.Firms.AsNoTracking()
            .Where(f => isIssued ? f.IsCustomer : f.IsSupplier)
            .OrderBy(f => f.Name).ToListAsync();
    }
}
