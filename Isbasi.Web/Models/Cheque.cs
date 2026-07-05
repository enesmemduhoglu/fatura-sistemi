using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum ChequeType
{
    [Display(Name = "Alınan Çek")] Received = 0,
    [Display(Name = "Verilen Çek")] Issued = 1
}

public enum ChequeStatus
{
    [Display(Name = "Portföyde")] Portfolio = 0,
    [Display(Name = "Tahsil Edildi")] Cleared = 1,
    [Display(Name = "Karşılıksız")] Bounced = 2
}

/// <summary>
/// Müşteriden alınan ya da tedarikçiye verilen çek. Tahsil/ödeme anında
/// (Cleared) seçilen kasa/banka bakiyesine yansır: alınan çek giriş, verilen çıkış.
/// </summary>
public class Cheque : ISoftDeletable
{
    public int Id { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    [Display(Name = "Çek Tipi")]
    public ChequeType Type { get; set; }

    [Display(Name = "Firma")]
    public int FirmId { get; set; }
    public Firm? Firm { get; set; }

    [Display(Name = "Çek Numarası")]
    [Required(ErrorMessage = "Çek numarası zorunludur.")]
    public string ChequeNumber { get; set; } = "";

    [Display(Name = "Banka")]
    public string? BankName { get; set; }

    [Display(Name = "Düzenleme Tarihi")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [Display(Name = "Vade Tarihi")]
    public DateTime DueDate { get; set; } = DateTime.Today;

    [Display(Name = "Tutar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal Amount { get; set; }

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Durum")]
    public ChequeStatus Status { get; set; } = ChequeStatus.Portfolio;

    // Tahsil/ödeme bilgileri (Status = Cleared iken zorunlu)
    [Display(Name = "Tahsil Tarihi")]
    public DateTime? ClearedDate { get; set; }

    [Display(Name = "Hesap Tipi")]
    public PaymentAccountType AccountType { get; set; } = PaymentAccountType.Bank;

    public int? SafeId { get; set; }
    public Safe? Safe { get; set; }

    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public bool IsReceived => Type == ChequeType.Received;
}
