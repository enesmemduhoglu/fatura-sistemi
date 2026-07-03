using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Data;

/// <summary>
/// Vadesi gelen aktif tekrarlama planlarından fatura üretir. Arka plan işi yoktur;
/// dashboard ve tekrarlayan faturalar sayfası açıldığında çağrılır (lazy üretim).
/// Geçmişte kalan dönemler tek tek üretilir (catch-up), plan başına 24 dönem sınırı vardır.
/// </summary>
public static class RecurringGenerator
{
    private const int MaxCatchUpPerPlan = 24;

    public static async Task<int> GenerateDue(AppDbContext db)
    {
        var today = DateTime.Today;
        var duePlans = await db.RecurringPlans
            .Include(p => p.SourceInvoice)!.ThenInclude(i => i!.Lines)
            .Where(p => p.IsActive && p.NextRunDate <= today)
            .ToListAsync();

        int generated = 0;
        foreach (var plan in duePlans)
        {
            var source = plan.SourceInvoice;
            if (source == null) continue;

            int safety = 0;
            while (plan.NextRunDate <= today && safety++ < MaxCatchUpPerPlan)
            {
                if (plan.EndDate.HasValue && plan.NextRunDate > plan.EndDate.Value)
                {
                    plan.IsActive = false;
                    break;
                }

                var invoice = new Invoice
                {
                    Type = source.Type,
                    FirmId = source.FirmId,
                    InvoiceDate = plan.NextRunDate,
                    Currency = source.Currency,
                    Category = source.Category,
                    Description = $"{source.InvoiceNumber} numaralı belgeden tekrarlayan fatura. {source.Description}".Trim(),
                    DeliveryAddress = source.DeliveryAddress,
                    GeneralDiscountValue = source.GeneralDiscountValue,
                    GeneralDiscountType = source.GeneralDiscountType,
                    Lines = source.Lines.Select(l => new InvoiceLine
                    {
                        ProductId = l.ProductId,
                        ServiceId = l.ServiceId,
                        ItemName = l.ItemName,
                        Quantity = l.Quantity,
                        Unit = l.Unit,
                        UnitPrice = l.UnitPrice,
                        VatRate = l.VatRate,
                        DiscountValue = l.DiscountValue,
                        DiscountType = l.DiscountType
                    }).ToList()
                };
                if (source.DueDate.HasValue)
                    invoice.DueDate = plan.NextRunDate + (source.DueDate.Value.Date - source.InvoiceDate.Date);

                InvoiceCalculator.Calculate(invoice);
                invoice.InvoiceNumber = await InvoiceNumbers.Next(db, "ISB");

                db.Invoices.Add(invoice);
                await db.SaveChangesAsync();   // numara serisi bir sonraki üretimde doğru ilerlesin

                plan.LastRunDate = plan.NextRunDate;
                plan.NextRunDate = plan.Advance(plan.NextRunDate);
                plan.GeneratedCount++;
                generated++;
            }

            if (plan.EndDate.HasValue && plan.NextRunDate > plan.EndDate.Value)
                plan.IsActive = false;
        }

        await db.SaveChangesAsync();
        return generated;
    }
}
