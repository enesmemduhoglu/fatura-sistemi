using Isbasi.Web.Models;

namespace Isbasi.Web.Data;

/// <summary>
/// Fatura toplamlarını hesaplar. wwwroot/js/invoice-form.js ile aynı mantığı uygular;
/// kayıt sırasında sunucu tarafında yeniden hesaplanarak istemciye güvenilmez.
/// </summary>
public static class InvoiceCalculator
{
    public static void Calculate(Invoice invoice)
    {
        bool vatIncluded = invoice.Type == InvoiceType.SalesRetail;

        decimal subTotal = 0;          // satır indirimi düşülmüş, KDV hariç satır toplamları
        decimal lineDiscountTotal = 0; // KDV hariç bazda satır indirimleri

        foreach (var line in invoice.Lines)
        {
            decimal gross = Math.Round(line.Quantity * line.UnitPrice, 2);
            decimal discount = line.DiscountType == DiscountType.Rate
                ? Math.Round(gross * line.DiscountValue / 100m, 2)
                : line.DiscountValue;
            decimal net = gross - discount;

            decimal baseAmount, vat;
            if (vatIncluded)
            {
                // KDV dahil girişte matrah içinden ayrıştırılır
                baseAmount = Math.Round(net / (1 + line.VatRate / 100m), 2);
                vat = net - baseAmount;
                discount = Math.Round(discount / (1 + line.VatRate / 100m), 2);
            }
            else
            {
                baseAmount = net;
                vat = Math.Round(net * line.VatRate / 100m, 2);
            }

            line.VatAmount = vat;
            line.LineTotal = baseAmount;
            subTotal += baseAmount;
            lineDiscountTotal += discount;
        }

        // Genel indirim ara toplama uygulanır, KDV aynı oranda azaltılır
        decimal generalDiscount = invoice.GeneralDiscountType == DiscountType.Rate
            ? Math.Round(subTotal * invoice.GeneralDiscountValue / 100m, 2)
            : invoice.GeneralDiscountValue;
        if (generalDiscount > subTotal) generalDiscount = subTotal;

        decimal ratio = subTotal > 0 ? (subTotal - generalDiscount) / subTotal : 1m;
        decimal vatTotal = Math.Round(invoice.Lines.Sum(l => l.VatAmount) * ratio, 2);
        decimal total = subTotal - generalDiscount;

        invoice.SubTotal = subTotal;
        invoice.DiscountTotal = lineDiscountTotal + generalDiscount;
        invoice.VatTotal = vatTotal;
        invoice.GrandTotal = total + vatTotal;
    }
}
