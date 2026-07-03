using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

/// <summary>Belge numarası serileri: {SERİ}{YIL}{9 haneli sıra} (ISB, SIP, SMM).</summary>
public static class InvoiceNumbers
{
    public static async Task<string> Next(AppDbContext db, string series)
    {
        var year = DateTime.Today.Year;
        var prefix = $"{series}{year}";
        var last = await db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out int lastNo))
            next = lastNo + 1;
        return $"{prefix}{next:D9}";
    }
}
