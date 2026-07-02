using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

public record CashBalances(Dictionary<int, decimal> SafeBalances, Dictionary<int, decimal> BankBalances)
{
    public decimal SafeBalanceOf(Safe safe) => SafeBalances.GetValueOrDefault(safe.Id, safe.OpeningBalance);
    public decimal BankBalanceOf(BankAccount bank) => BankBalances.GetValueOrDefault(bank.Id, bank.OpeningBalance);
}

/// <summary>
/// Güncel kasa/banka bakiyeleri: açılış bakiyesi + tahsilatlar (satış) − ödemeler (alış/gider).
/// SQLite decimal üzerinde SQL Sum yapamadığı için toplamlar bellekte alınır.
/// </summary>
public static class CashBalanceCalculator
{
    public static async Task<CashBalances> Compute(AppDbContext db)
    {
        var safes = await db.Safes.AsNoTracking().ToListAsync();
        var banks = await db.BankAccounts.AsNoTracking().ToListAsync();
        var payments = await db.Payments.AsNoTracking()
            .Include(p => p.Invoice)
            .Select(p => new { p.SafeId, p.BankAccountId, p.Amount, p.Invoice!.Type })
            .ToListAsync();

        var safeBalances = safes.ToDictionary(s => s.Id, s => s.OpeningBalance);
        var bankBalances = banks.ToDictionary(b => b.Id, b => b.OpeningBalance);

        foreach (var payment in payments)
        {
            bool isIncoming = payment.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail;
            decimal delta = isIncoming ? payment.Amount : -payment.Amount;

            if (payment.SafeId.HasValue && safeBalances.ContainsKey(payment.SafeId.Value))
                safeBalances[payment.SafeId.Value] += delta;
            else if (payment.BankAccountId.HasValue && bankBalances.ContainsKey(payment.BankAccountId.Value))
                bankBalances[payment.BankAccountId.Value] += delta;
        }

        return new CashBalances(safeBalances, bankBalances);
    }
}
