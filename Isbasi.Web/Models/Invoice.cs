using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum InvoiceType
{
    [Display(Name = "Toptan Satış Faturası (KDV Hariç)")] SalesWholesale = 0,
    [Display(Name = "Perakende Satış Faturası (KDV Dahil)")] SalesRetail = 1,
    [Display(Name = "Alış Faturası")] Purchase = 2
}

public enum InvoiceStatus
{
    [Display(Name = "Açık")] Open = 0,
    [Display(Name = "Ödendi")] Paid = 1
}

public class Invoice
{
    public int Id { get; set; }

    // Boş bırakılırsa kayıt sırasında otomatik numara verilir; boş string null'a
    // dönüşüp implicit [Required] tetiklemesin diye ConvertEmptyStringToNull kapalı
    [Display(Name = "Fatura Numarası")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string InvoiceNumber { get; set; } = "";

    public InvoiceType Type { get; set; }

    public int FirmId { get; set; }
    public Firm? Firm { get; set; }

    [Display(Name = "Fatura Tarihi")]
    public DateTime InvoiceDate { get; set; } = DateTime.Now;

    [Display(Name = "Sevk Tarihi")]
    public DateTime? ShipmentDate { get; set; }

    [Display(Name = "Vade Tarihi")]
    public DateTime? DueDate { get; set; }

    [Display(Name = "Döviz")]
    public string Currency { get; set; } = "TL";

    [Display(Name = "Kategori")]
    public string? Category { get; set; }

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Kayıt Durumu")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    // Genel indirim (satır indirimlerinden ayrı, ara toplama uygulanır)
    public decimal GeneralDiscountValue { get; set; }
    public DiscountType GeneralDiscountType { get; set; } = DiscountType.Rate;

    [Display(Name = "Ara Toplam")]
    public decimal SubTotal { get; set; }

    [Display(Name = "Toplam İndirim")]
    public decimal DiscountTotal { get; set; }

    [Display(Name = "Toplam KDV Tutarı")]
    public decimal VatTotal { get; set; }

    [Display(Name = "Genel Toplam")]
    public decimal GrandTotal { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();

    public bool IsSales => Type != InvoiceType.Purchase;
}
