using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

/// <summary>
/// Çöp kutusu: yumuşak silinen belgeler (fatura/sipariş/iade, tahsilat-ödeme, çek, SMM)
/// burada listelenir, geri alınır ya da kalıcı olarak silinir. Kartlar (firma, ürün,
/// kasa...) çöpe gelmez; onlar bağlı kaydı yokken sert silinir. Raporlar da gelmez:
/// rapor kayıtlardan hesaplanır, geri alınan belge rapora kendiliğinden döner.
/// </summary>
[Route("trash")]
public class TrashController : Controller
{
    private readonly AppDbContext _db;

    public TrashController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? kind)
    {
        var rows = new List<TrashRow>();

        if (kind is null or "" or "invoice")
        {
            var invoices = await _db.Invoices.IgnoreQueryFilters().AsNoTracking()
                .Include(i => i.Firm)
                .Where(i => i.IsDeleted)
                .ToListAsync();
            rows.AddRange(invoices.Select(i => new TrashRow
            {
                Kind = "invoice",
                KindLabel = InvoiceTypeLabel(i.Type),
                Id = i.Id,
                Title = i.InvoiceNumber,
                Detail = i.Firm?.Name,
                AmountTry = i.GrandTotalTry,
                DeletedAt = i.DeletedAt
            }));
        }

        if (kind is null or "" or "payment")
        {
            var payments = await _db.Payments.IgnoreQueryFilters().AsNoTracking()
                .Include(p => p.Invoice)!.ThenInclude(i => i!.Firm)
                .Where(p => p.IsDeleted)
                .ToListAsync();
            rows.AddRange(payments.Select(p => new TrashRow
            {
                Kind = "payment",
                KindLabel = p.Invoice?.IsCashIncoming == false ? "Ödeme" : "Tahsilat",
                Id = p.Id,
                Title = p.Invoice?.InvoiceNumber ?? "-",
                Detail = p.Invoice?.Firm?.Name,
                AmountTry = p.Amount,
                DeletedAt = p.DeletedAt
            }));
        }

        if (kind is null or "" or "cheque")
        {
            var cheques = await _db.Cheques.IgnoreQueryFilters().AsNoTracking()
                .Include(c => c.Firm)
                .Where(c => c.IsDeleted)
                .ToListAsync();
            rows.AddRange(cheques.Select(c => new TrashRow
            {
                Kind = "cheque",
                KindLabel = c.IsReceived ? "Alınan Çek" : "Verilen Çek",
                Id = c.Id,
                Title = c.ChequeNumber,
                Detail = c.Firm?.Name,
                AmountTry = c.Amount,
                DeletedAt = c.DeletedAt
            }));
        }

        if (kind is null or "" or "receipt")
        {
            var receipts = await _db.FreelanceReceipts.IgnoreQueryFilters().AsNoTracking()
                .Include(r => r.Firm)
                .Where(r => r.IsDeleted)
                .ToListAsync();
            rows.AddRange(receipts.Select(r => new TrashRow
            {
                Kind = "receipt",
                KindLabel = r.IsIssued ? "Verilen SMM" : "Alınan SMM",
                Id = r.Id,
                Title = r.ReceiptNumber,
                Detail = r.Firm?.Name,
                AmountTry = r.NetAmount,
                DeletedAt = r.DeletedAt
            }));
        }

        ViewBag.Kind = kind;
        return View(rows.OrderByDescending(r => r.DeletedAt).ToList());
    }

    [HttpPost("restore/{kind}/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string kind, int id)
    {
        switch (kind)
        {
            case "invoice":
            {
                var invoice = await _db.Invoices.IgnoreQueryFilters()
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i => i.Id == id && i.IsDeleted);
                if (invoice == null) return NotFound();

                invoice.IsDeleted = false;
                invoice.DeletedAt = null;
                // Faturayla birlikte çöpe giden tahsilatlar da döner
                foreach (var payment in invoice.Payments.Where(p => p.IsDeleted))
                {
                    payment.IsDeleted = false;
                    payment.DeletedAt = null;
                }
                UpdateStatus(invoice);
                TempData["Success"] = $"{invoice.InvoiceNumber} geri alındı.";
                break;
            }
            case "payment":
            {
                var payment = await _db.Payments.IgnoreQueryFilters()
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
                if (payment == null) return NotFound();
                if (payment.Invoice!.IsDeleted)
                {
                    TempData["Error"] = "Bu kaydın faturası da çöp kutusunda; önce faturayı geri alın.";
                    return RedirectToAction(nameof(Index));
                }

                payment.IsDeleted = false;
                payment.DeletedAt = null;
                await _db.SaveChangesAsync();

                // Navigation yerine sorguyla toplanır (izlenen entity fixup'ı toplamı bozabilir;
                // SQLite decimal Sum kısıtı nedeniyle bellekte)
                var invoice = await _db.Invoices.FirstAsync(i => i.Id == payment.InvoiceId);
                decimal paid = (await _db.Payments.Where(p => p.InvoiceId == invoice.Id)
                    .Select(p => p.Amount).ToListAsync()).Sum();
                invoice.Status = paid >= invoice.GrandTotalTry && invoice.GrandTotalTry > 0
                    ? InvoiceStatus.Paid
                    : InvoiceStatus.Open;
                TempData["Success"] = "Tahsilat/ödeme kaydı geri alındı.";
                break;
            }
            case "cheque":
            {
                var cheque = await _db.Cheques.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);
                if (cheque == null) return NotFound();
                cheque.IsDeleted = false;
                cheque.DeletedAt = null;
                TempData["Success"] = $"{cheque.ChequeNumber} numaralı çek geri alındı.";
                break;
            }
            case "receipt":
            {
                var receipt = await _db.FreelanceReceipts.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted);
                if (receipt == null) return NotFound();
                receipt.IsDeleted = false;
                receipt.DeletedAt = null;
                TempData["Success"] = $"{receipt.ReceiptNumber} geri alındı.";
                break;
            }
            default:
                return NotFound();
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("purge/{kind}/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purge(string kind, int id, [FromServices] AttachmentStorage storage)
    {
        switch (kind)
        {
            case "invoice":
            {
                var invoice = await _db.Invoices.IgnoreQueryFilters()
                    .Include(i => i.Attachments)
                    .FirstOrDefaultAsync(i => i.Id == id && i.IsDeleted);
                if (invoice == null) return NotFound();

                _db.Invoices.Remove(invoice);   // satır/tahsilat/ek kayıtları cascade gider
                await _db.SaveChangesAsync();
                foreach (var attachment in invoice.Attachments)
                    storage.Delete(attachment.StoredName);
                TempData["Success"] = $"{invoice.InvoiceNumber} kalıcı olarak silindi.";
                break;
            }
            case "payment":
            {
                var payment = await _db.Payments.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
                if (payment == null) return NotFound();
                _db.Payments.Remove(payment);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Tahsilat/ödeme kaydı kalıcı olarak silindi.";
                break;
            }
            case "cheque":
            {
                var cheque = await _db.Cheques.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);
                if (cheque == null) return NotFound();
                _db.Cheques.Remove(cheque);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"{cheque.ChequeNumber} numaralı çek kalıcı olarak silindi.";
                break;
            }
            case "receipt":
            {
                var receipt = await _db.FreelanceReceipts.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted);
                if (receipt == null) return NotFound();
                _db.FreelanceReceipts.Remove(receipt);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"{receipt.ReceiptNumber} kalıcı olarak silindi.";
                break;
            }
            default:
                return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    private static void UpdateStatus(Invoice invoice)
        => invoice.Status = invoice.PaidTotal >= invoice.GrandTotalTry && invoice.GrandTotalTry > 0
            ? InvoiceStatus.Paid
            : InvoiceStatus.Open;

    private static string InvoiceTypeLabel(InvoiceType type) => type switch
    {
        InvoiceType.Purchase => "Alış Faturası",
        InvoiceType.Expense => "Gider Faturası",
        InvoiceType.SalesOrder => "Satış Siparişi",
        InvoiceType.PurchaseOrder => "Alış Siparişi",
        InvoiceType.SalesReturn => "Satış İade Faturası",
        InvoiceType.PurchaseReturn => "Alış İade Faturası",
        _ => "Satış Faturası"
    };
}
