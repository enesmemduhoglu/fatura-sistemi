using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum DiscountType
{
    [Display(Name = "Oran")] Rate = 0,
    [Display(Name = "Tutar")] Amount = 1
}

public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public int? ServiceId { get; set; }
    public Service? Service { get; set; }

    [Display(Name = "Ürün")]
    public string ItemName { get; set; } = "";

    [Display(Name = "Miktar")]
    public decimal Quantity { get; set; } = 1;

    [Display(Name = "Birimi")]
    public string Unit { get; set; } = "Adet";

    // KDV Hariç faturada KDV'siz, KDV Dahil faturada KDV'li girilir
    [Display(Name = "Birim Fiyat")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Vergi (%)")]
    public decimal VatRate { get; set; } = 20;

    [Display(Name = "Vergi Tutarı")]
    public decimal VatAmount { get; set; }

    [Display(Name = "İndirim")]
    public decimal DiscountValue { get; set; }

    [Display(Name = "İndirim Tipi")]
    public DiscountType DiscountType { get; set; } = DiscountType.Rate;

    // KDV hariç, indirim düşülmüş satır tutarı
    [Display(Name = "Tutar")]
    public decimal LineTotal { get; set; }
}
