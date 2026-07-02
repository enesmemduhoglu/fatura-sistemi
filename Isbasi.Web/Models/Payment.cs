using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum PaymentAccountType
{
    [Display(Name = "Kasa")] Safe = 0,
    [Display(Name = "Banka")] Bank = 1
}

/// <summary>
/// Fatura tahsilatı ya da ödemesi. Yön faturanın tipinden gelir:
/// satış faturasında para girişi (tahsilat), alış/gider faturasında para çıkışı (ödeme).
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Display(Name = "Tarih")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Tutar")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Tutar sıfırdan büyük olmalıdır.")]
    public decimal Amount { get; set; }

    [Display(Name = "Hesap Tipi")]
    public PaymentAccountType AccountType { get; set; } = PaymentAccountType.Safe;

    public int? SafeId { get; set; }
    public Safe? Safe { get; set; }

    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }
}
