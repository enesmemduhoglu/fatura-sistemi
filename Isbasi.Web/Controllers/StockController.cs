using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("stock")]
public class StockController : Controller
{
    private readonly AppDbContext _db;

    public StockController(AppDbContext db) => _db = db;

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(int? productId, DateTime? start, DateTime? end)
    {
        // Yalnızca stok hesabına giren belgeler (StockCalculator ile aynı küme):
        // siparişler stok hareketi değildir, faturaya dönüştüğünde listelenir
        var query = _db.InvoiceLines.AsNoTracking()
            .Include(l => l.Product)
            .Include(l => l.Invoice)!.ThenInclude(i => i!.Firm)
            .Where(l => l.ProductId != null
                && (l.Invoice!.Type == InvoiceType.SalesWholesale
                    || l.Invoice.Type == InvoiceType.SalesRetail
                    || l.Invoice.Type == InvoiceType.Purchase
                    || l.Invoice.Type == InvoiceType.SalesReturn
                    || l.Invoice.Type == InvoiceType.PurchaseReturn));

        if (productId.HasValue) query = query.Where(l => l.ProductId == productId.Value);
        if (start.HasValue) query = query.Where(l => l.Invoice!.InvoiceDate >= start.Value);
        if (end.HasValue) query = query.Where(l => l.Invoice!.InvoiceDate < end.Value.AddDays(1));

        var lines = await query
            .OrderByDescending(l => l.Invoice!.InvoiceDate)
            .ThenByDescending(l => l.Id)
            .ToListAsync();

        ViewBag.Products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        ViewBag.Stocks = await StockCalculator.Compute(_db);
        ViewBag.ProductId = productId;
        ViewBag.Start = start;
        ViewBag.End = end;
        return View(lines);
    }
}
