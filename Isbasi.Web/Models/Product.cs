using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public class Product
{
    public int Id { get; set; }

    [Display(Name = "Ürün Kodu")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [Display(Name = "Ürün Adı")]
    public string Name { get; set; } = "";

    [Display(Name = "Birimi")]
    public string Unit { get; set; } = "Adet";

    [Display(Name = "Satış Fiyatı")]
    public decimal SalePrice { get; set; }

    [Display(Name = "Alış Fiyatı")]
    public decimal PurchasePrice { get; set; }

    [Display(Name = "KDV Oranı (%)")]
    public decimal VatRate { get; set; } = 20;

    // Açılış stoku; güncel stok fatura hareketleriyle StockCalculator'da hesaplanır
    [Display(Name = "Açılış Stok Miktarı")]
    public decimal StockAmount { get; set; }
}
