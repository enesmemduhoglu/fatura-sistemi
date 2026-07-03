using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

/// <summary>Tek satırlık firma ayarları; topbar ve fatura çıktısındaki satıcı bilgileri buradan gelir.</summary>
public class CompanySettings
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Firma ünvanı zorunludur.")]
    [Display(Name = "Firma Ünvanı")]
    public string CompanyName { get; set; } = "";

    [Display(Name = "Yetkili Adı Soyadı")]
    public string? OwnerName { get; set; }

    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [Display(Name = "İl")]
    public string? City { get; set; }

    [Display(Name = "İlçe")]
    public string? District { get; set; }

    [Display(Name = "Vergi Dairesi")]
    public string? TaxOffice { get; set; }

    [Display(Name = "Vergi No/T.C. Kimlik No")]
    public string? TaxNumber { get; set; }

    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Display(Name = "E-Posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string? Email { get; set; }

    // Kağıt fatura şablonu: yazdırma sayfalarının görünümünü belirler
    [Display(Name = "Şablon")]
    public string PrintTemplate { get; set; } = "classic";   // classic | modern | plain

    [Display(Name = "Tema Rengi")]
    public string PrintAccentColor { get; set; } = "#e8112d";

    [Display(Name = "Fatura Dipnotu")]
    public string? PrintFooterNote { get; set; }
}
