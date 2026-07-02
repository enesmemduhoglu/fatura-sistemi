using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum FirmKind
{
    [Display(Name = "Kurumsal")] Corporate = 0,
    [Display(Name = "Bireysel")] Individual = 1
}

public class Firm
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Firma ünvanı / adı soyadı zorunludur.")]
    [Display(Name = "Firma Ünvanı / Adı Soyadı")]
    public string Name { get; set; } = "";

    [Display(Name = "Firma Tipi")]
    public FirmKind Kind { get; set; } = FirmKind.Corporate;

    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [Display(Name = "Ülke")]
    public string? Country { get; set; } = "Türkiye";

    [Display(Name = "İl")]
    public string? City { get; set; }

    [Display(Name = "İlçe")]
    public string? District { get; set; }

    [Display(Name = "Vergi Dairesi")]
    public string? TaxOffice { get; set; }

    [Display(Name = "Vergi No/T.C. Kimlik No")]
    public string? TaxNumber { get; set; }

    [Display(Name = "E-Posta")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string? Email { get; set; }

    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Display(Name = "Müşteri")]
    public bool IsCustomer { get; set; } = true;

    [Display(Name = "Tedarikçi")]
    public bool IsSupplier { get; set; }

    public List<Invoice> Invoices { get; set; } = new();
}
