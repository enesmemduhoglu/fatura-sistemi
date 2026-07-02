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
        var query = _db.InvoiceLines.AsNoTracking()
            .Include(l => l.Product)
            .Include(l => l.Invoice)!.ThenInclude(i => i!.Firm)
            .Where(l => l.ProductId != null && l.Invoice!.Type != InvoiceType.Expense);

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
