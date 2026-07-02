using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [Display(Name = "E-Posta")]
    public string Email { get; set; } = "";

    [Display(Name = "Ad Soyad")]
    public string DisplayName { get; set; } = "";

    // "salt.hash" biçiminde PBKDF2 (bkz. Data/PasswordHasher)
    public string PasswordHash { get; set; } = "";
}
