using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public class Safe
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kasa adı zorunludur.")]
    [Display(Name = "Kasa Adı")]
    public string Name { get; set; } = "";

    [Display(Name = "Döviz")]
    public string Currency { get; set; } = "TL";

    [Display(Name = "Açılış Bakiyesi")]
    public decimal OpeningBalance { get; set; }
}
