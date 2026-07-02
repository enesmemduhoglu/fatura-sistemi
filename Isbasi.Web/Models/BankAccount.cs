using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public class BankAccount
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Hesap adı zorunludur.")]
    [Display(Name = "Hesap Adı")]
    public string Name { get; set; } = "";

    [Display(Name = "Banka")]
    public string? BankName { get; set; }

    [Display(Name = "Şube")]
    public string? Branch { get; set; }

    [Display(Name = "IBAN")]
    public string? Iban { get; set; }

    [Display(Name = "Döviz")]
    public string Currency { get; set; } = "TL";

    [Display(Name = "Açılış Bakiyesi")]
    public decimal OpeningBalance { get; set; }
}
