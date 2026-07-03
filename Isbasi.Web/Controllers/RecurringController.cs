using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("recurring")]
public class RecurringController : Controller
{
    private readonly AppDbContext _db;

    public RecurringController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        int generated = await RecurringGenerator.GenerateDue(_db);
        if (generated > 0)
            TempData["Success"] = $"{generated} tekrarlayan fatura otomatik oluşturuldu.";

        var plans = await _db.RecurringPlans.AsNoTracking()
            .Include(p => p.SourceInvoice)!.ThenInclude(i => i!.Firm)
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.NextRunDate)
            .ToListAsync();
        return View(plans);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(int invoiceId)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return NotFound();
        if (invoice.IsOrder)
        {
            TempData["Error"] = "Siparişler için tekrarlama planı oluşturulamaz.";
            return Redirect(invoice.Type == InvoiceType.PurchaseOrder ? "/invoice/purchaseorders" : "/invoice/orders");
        }

        var existing = await _db.RecurringPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SourceInvoiceId == invoiceId);
        if (existing != null)
        {
            TempData["Error"] = $"{invoice.InvoiceNumber} için zaten bir tekrarlama planı var.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Invoice = invoice;
        return View(new RecurringPlan
        {
            SourceInvoiceId = invoiceId,
            NextRunDate = invoice.InvoiceDate.Date == DateTime.Today
                ? DateTime.Today.AddMonths(1)
                : DateTime.Today
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecurringPlan model)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == model.SourceInvoiceId);
        if (invoice == null || invoice.IsOrder) return NotFound();

        if (model.EndDate.HasValue && model.EndDate.Value < model.NextRunDate)
            ModelState.AddModelError("EndDate", "Bitiş tarihi ilk fatura tarihinden önce olamaz.");
        if (await _db.RecurringPlans.AnyAsync(p => p.SourceInvoiceId == model.SourceInvoiceId))
            ModelState.AddModelError("", "Bu belge için zaten bir tekrarlama planı var.");

        if (!ModelState.IsValid)
        {
            ViewBag.Invoice = await _db.Invoices.AsNoTracking().Include(i => i.Firm)
                .FirstAsync(i => i.Id == model.SourceInvoiceId);
            return View(model);
        }

        model.Id = 0;
        model.IsActive = true;
        _db.RecurringPlans.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Tekrarlama planı oluşturuldu. Vadesi gelen faturalar otomatik üretilir.";
        return RedirectToAction(nameof(Index));
    }

    // Sonraki dönemi beklemeden hemen bir fatura üretir
    [HttpPost("runnow/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        var plan = await _db.RecurringPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();

        plan.NextRunDate = DateTime.Today;
        plan.IsActive = true;
        await _db.SaveChangesAsync();

        int generated = await RecurringGenerator.GenerateDue(_db);
        TempData["Success"] = generated > 0 ? "Fatura oluşturuldu." : "Üretilecek dönem bulunamadı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var plan = await _db.RecurringPlans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();

        plan.IsActive = !plan.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = plan.IsActive ? "Plan aktifleştirildi." : "Plan duraklatıldı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _db.RecurringPlans.FindAsync(id);
        if (plan == null) return NotFound();

        _db.RecurringPlans.Remove(plan);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Tekrarlama planı silindi (üretilmiş faturalar korunur).";
        return RedirectToAction(nameof(Index));
    }
}
