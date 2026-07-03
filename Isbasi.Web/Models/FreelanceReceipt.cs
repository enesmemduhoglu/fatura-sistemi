using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum ReceiptType
{
    [Display(Name = "Verilen Serbest Meslek Makbuzu")] Issued = 0,
    [Display(Name = "Alınan Serbest Meslek Makbuzu")] Received = 1
}

/// <summary>
/// Serbest meslek makbuzu. Brüt ücret üzerinden stopaj kesilir, KDV eklenir:
/// net (tahsil edilecek) = brüt − stopaj + KDV. Verilen makbuz gelir (KDV'si
/// hesaplanan KDV'ye), alınan makbuz gider (KDV'si indirilecek KDV'ye) sayılır.
/// </summary>
public class FreelanceReceipt
{
    public int Id { get; set; }

    [Display(Name = "Makbuz Tipi")]
    public ReceiptType Type { get; set; }

    [Display(Name = "Firma")]
    public int FirmId { get; set; }
    public Firm? Firm { get; set; }

    // Boş bırakılırsa kayıt sırasında SMM serisinden otomatik verilir
    [Display(Name = "Makbuz Numarası")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string ReceiptNumber { get; set; } = "";

    [Display(Name = "Tarih")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Brüt Ücret")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Brüt ücret sıfırdan büyük olmalıdır.")]
    public decimal GrossAmount { get; set; }

    [Display(Name = "Stopaj Oranı (%)")]
    [Range(0, 100, ErrorMessage = "Stopaj oranı 0-100 arasında olmalıdır.")]
    public decimal StopajRate { get; set; } = 20;

    [Display(Name = "KDV Oranı (%)")]
    [Range(0, 100, ErrorMessage = "KDV oranı 0-100 arasında olmalıdır.")]
    public decimal VatRate { get; set; } = 20;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Kayıt Durumu")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    public decimal StopajAmount => Math.Round(GrossAmount * StopajRate / 100m, 2);
    public decimal VatAmount => Math.Round(GrossAmount * VatRate / 100m, 2);

    [Display(Name = "Net (Tahsil Edilecek)")]
    public decimal NetAmount => GrossAmount - StopajAmount + VatAmount;

    public bool IsIssued => Type == ReceiptType.Issued;
}
