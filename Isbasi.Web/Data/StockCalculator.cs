using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

/// <summary>
/// Güncel stok = açılış stok miktarı + alış faturası girişleri − satış faturası çıkışları.
/// Gider faturalarında ürün satırı bulunmaz. Toplamlar bellekte alınır (SQLite decimal Sum kısıtı).
/// </summary>
public static class StockCalculator
{
    public static async Task<Dictionary<int, decimal>> Compute(AppDbContext db)
    {
        var products = await db.Products.AsNoTracking().Select(p => new { p.Id, p.StockAmount }).ToListAsync();
        var lines = await db.InvoiceLines.AsNoTracking()
            .Where(l => l.ProductId != null)
            .Select(l => new { ProductId = l.ProductId!.Value, l.Quantity, l.Invoice!.Type })
            .ToListAsync();

        var stocks = products.ToDictionary(p => p.Id, p => p.StockAmount);
        foreach (var line in lines)
        {
            if (!stocks.ContainsKey(line.ProductId)) continue;
            if (line.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail)
                stocks[line.ProductId] -= line.Quantity;
            else if (line.Type == InvoiceType.Purchase)
                stocks[line.ProductId] += line.Quantity;
        }
        return stocks;
    }
}
