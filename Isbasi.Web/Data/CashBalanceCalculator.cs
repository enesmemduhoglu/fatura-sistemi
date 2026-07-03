using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

public record CashBalances(Dictionary<int, decimal> SafeBalances, Dictionary<int, decimal> BankBalances)
{
    public decimal SafeBalanceOf(Safe safe) => SafeBalances.GetValueOrDefault(safe.Id, safe.OpeningBalance);
    public decimal BankBalanceOf(BankAccount bank) => BankBalances.GetValueOrDefault(bank.Id, bank.OpeningBalance);
}

/// <summary>
/// Güncel kasa/banka bakiyeleri: açılış bakiyesi + tahsilatlar (satış) − ödemeler (alış/gider)
/// + tahsil edilen alınan çekler − ödenen verilen çekler.
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
        var clearedCheques = await db.Cheques.AsNoTracking()
            .Where(c => c.Status == ChequeStatus.Cleared)
            .Select(c => new { c.SafeId, c.BankAccountId, c.Amount, c.Type })
            .ToListAsync();

        var safeBalances = safes.ToDictionary(s => s.Id, s => s.OpeningBalance);
        var bankBalances = banks.ToDictionary(b => b.Id, b => b.OpeningBalance);

        foreach (var payment in payments)
        {
            bool isIncoming = payment.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail;
            Apply(payment.SafeId, payment.BankAccountId, isIncoming ? payment.Amount : -payment.Amount);
        }

        foreach (var cheque in clearedCheques)
            Apply(cheque.SafeId, cheque.BankAccountId, cheque.Type == ChequeType.Received ? cheque.Amount : -cheque.Amount);

        return new CashBalances(safeBalances, bankBalances);

        void Apply(int? safeId, int? bankAccountId, decimal delta)
        {
            if (safeId.HasValue && safeBalances.ContainsKey(safeId.Value))
                safeBalances[safeId.Value] += delta;
            else if (bankAccountId.HasValue && bankBalances.ContainsKey(bankAccountId.Value))
                bankBalances[bankAccountId.Value] += delta;
        }
    }
}
