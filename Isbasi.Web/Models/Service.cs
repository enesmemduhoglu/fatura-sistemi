using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public class Service
{
    public int Id { get; set; }

    [Display(Name = "Hizmet Kodu")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Hizmet adı zorunludur.")]
    [Display(Name = "Hizmet Adı")]
    public string Name { get; set; } = "";

    [Display(Name = "Birimi")]
    public string Unit { get; set; } = "Adet";

    [Display(Name = "Birim Fiyat")]
    public decimal Price { get; set; }

    [Display(Name = "KDV Oranı (%)")]
    public decimal VatRate { get; set; } = 20;
}
