using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("cheque")]
public class ChequeController : Controller
{
    private readonly AppDbContext _db;

    public ChequeController(AppDbContext db) => _db = db;

    // kind: received | issued (Cheque.Type ile isim çakışıp ModelState'e sızmasın diye "type" değil)
    [HttpGet("")]
    public async Task<IActionResult> Index(string kind = "received", string? status = null, string? q = null)
    {
        var type = kind == "issued" ? ChequeType.Issued : ChequeType.Received;
        var query = _db.Cheques.AsNoTracking()
            .Include(c => c.Firm).Include(c => c.Safe).Include(c => c.BankAccount)
            .Where(c => c.Type == type);

        if (status == "Portfolio") query = query.Where(c => c.Status == ChequeStatus.Portfolio);
        else if (status == "Cleared") query = query.Where(c => c.Status == ChequeStatus.Cleared);
        else if (status == "Bounced") query = query.Where(c => c.Status == ChequeStatus.Bounced);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Firm!.Name.Contains(q) || c.ChequeNumber.Contains(q));

        ViewBag.Kind = kind == "issued" ? "issued" : "received";
        ViewBag.Status = status;
        ViewBag.Query = q;
        return View(await query.OrderBy(c => c.DueDate).ToListAsync());
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(int? id, string kind = "received")
    {
        Cheque cheque;
        if (id.HasValue)
        {
            var existing = await _db.Cheques.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id.Value);
            if (existing == null) return NotFound();
            cheque = existing;
        }
        else
        {
            cheque = new Cheque { Type = kind == "issued" ? ChequeType.Issued : ChequeType.Received };
        }

        await FillViewBags(cheque);
        return View(cheque);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Cheque model)
    {
        if (model.FirmId == 0)
            ModelState.AddModelError("FirmId", "Firma seçimi zorunludur.");

        if (model.Status == ChequeStatus.Cleared)
        {
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
            model.ClearedDate ??= DateTime.Today;
        }
        else
        {
            // Portföyde ya da karşılıksız: tahsil bilgileri temizlenir, bakiye etkilenmez
            model.ClearedDate = null;
            model.SafeId = null;
            model.BankAccountId = null;
        }

        if (!ModelState.IsValid)
        {
            await FillViewBags(model);
            return View("Edit", model);
        }

        if (model.Id == 0) _db.Cheques.Add(model);
        else _db.Update(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Çek kaydedildi.";
        return RedirectToAction(nameof(Index), new { kind = model.Type == ChequeType.Issued ? "issued" : "received" });
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cheque = await _db.Cheques.FindAsync(id);
        if (cheque == null) return NotFound();

        cheque.IsDeleted = true;
        cheque.DeletedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Çek çöp kutusuna taşındı.";
        return RedirectToAction(nameof(Index), new { kind = cheque.Type == ChequeType.Issued ? "issued" : "received" });
    }

    private async Task FillViewBags(Cheque cheque)
    {
        bool isReceived = cheque.Type == ChequeType.Received;
        ViewBag.Firms = await _db.Firms.AsNoTracking()
            .Where(f => isReceived ? f.IsCustomer : f.IsSupplier)
            .OrderBy(f => f.Name).ToListAsync();
        ViewBag.Safes = await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.Banks = await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
    }
}
