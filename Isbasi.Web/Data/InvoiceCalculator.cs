using Isbasi.Web.Models;

namespace Isbasi.Web.Data;

/// <summary>
/// Fatura toplamlarını hesaplar. wwwroot/js/invoice-form.js ile aynı mantığı uygular;
/// kayıt sırasında sunucu tarafında yeniden hesaplanarak istemciye güvenilmez.
/// </summary>
public static class InvoiceCalculator
{
    // Geçerli KDV tevkifat oranları; kod UI'daki seçenek metniyle aynıdır
    public static readonly string[] TevkifatCodes = { "2/10", "3/10", "4/10", "5/10", "7/10", "9/10", "10/10" };

    private static (decimal Num, decimal Den) TevkifatFraction(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || Array.IndexOf(TevkifatCodes, code) < 0) return (0m, 1m);
        var parts = code.Split('/');
        return (decimal.Parse(parts[0]), decimal.Parse(parts[1]));
    }

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

        // Stopaj matrahı: indirimler düşülmüş KDV hariç toplam ("Toplam" satırı)
        decimal stopajRate = Math.Clamp(invoice.StopajRate, 0m, 100m);
        decimal stopajTotal = Math.Round(total * stopajRate / 100m, 2);

        // KDV tevkifatı nihai KDV üzerinden kesilir; KDV toplamı tam kalır,
        // tevkif edilen kısım ödenecek tutardan düşülür
        var (num, den) = TevkifatFraction(invoice.TevkifatCode);
        if (num == 0) invoice.TevkifatCode = "";
        decimal tevkifatVat = Math.Round(vatTotal * num / den, 2);

        invoice.StopajRate = stopajRate;
        invoice.StopajTotal = stopajTotal;
        invoice.TevkifatVatTotal = tevkifatVat;
        invoice.SubTotal = subTotal;
        invoice.DiscountTotal = lineDiscountTotal + generalDiscount;
        invoice.VatTotal = vatTotal;
        // Genel Toplam = net ödenecek tutar (stopaj ve tevkif edilen KDV düşülmüş);
        // ödeme takibi (RemainingTotal, Ödendi durumu) bu tutar üzerinden yürür
        invoice.GrandTotal = total + vatTotal - stopajTotal - tevkifatVat;
    }
}
