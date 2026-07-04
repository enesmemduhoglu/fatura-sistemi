using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum InvoiceType
{
    [Display(Name = "Toptan Satış Faturası (KDV Hariç)")] SalesWholesale = 0,
    [Display(Name = "Perakende Satış Faturası (KDV Dahil)")] SalesRetail = 1,
    [Display(Name = "Alış Faturası")] Purchase = 2,
    [Display(Name = "Gider Faturası")] Expense = 3,
    [Display(Name = "Satış Siparişi")] SalesOrder = 4,
    [Display(Name = "Alış Siparişi")] PurchaseOrder = 5
}

public enum OrderStatus
{
    [Display(Name = "Bekliyor")] Waiting = 0,
    [Display(Name = "Faturalandı")] Invoiced = 1,
    [Display(Name = "İptal")] Cancelled = 2
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

    // 1 birim döviz = ? TL; TL belgelerde her zaman 1. Tutarlar belge dövizindedir,
    // raporlar ve ödeme takibi TL karşılığı (…Try) üzerinden yürür.
    [Display(Name = "Kur")]
    public decimal ExchangeRate { get; set; } = 1;

    [Display(Name = "Kategori")]
    public string? Category { get; set; }

    // "Diğer" sekmesi: irsaliye bilgileri ve farklı teslimat adresi
    [Display(Name = "İrsaliye Numarası")]
    public string? DeliveryNoteNumber { get; set; }

    [Display(Name = "İrsaliye Tarihi")]
    public DateTime? DeliveryNoteDate { get; set; }

    [Display(Name = "Teslimat Adresi")]
    public string? DeliveryAddress { get; set; }

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Kayıt Durumu")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    // Genel indirim (satır indirimlerinden ayrı, ara toplama uygulanır)
    public decimal GeneralDiscountValue { get; set; }
    public DiscountType GeneralDiscountType { get; set; } = DiscountType.Rate;

    // Stopaj: indirimler düşülmüş KDV hariç toplam üzerinden kesilir
    [Display(Name = "Stopaj Oranı (%)")]
    [Range(0, 100, ErrorMessage = "Stopaj oranı 0-100 arasında olmalıdır.")]
    public decimal StopajRate { get; set; }

    [Display(Name = "Stopaj Toplamı")]
    public decimal StopajTotal { get; set; }

    // KDV tevkifatı: "9/10" gibi kesir kodu, boş = tevkifat yok
    [Display(Name = "KDV Tevkifatı")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string TevkifatCode { get; set; } = "";

    [Display(Name = "Tevkif Edilen KDV")]
    public decimal TevkifatVatTotal { get; set; }

    [Display(Name = "Ara Toplam")]
    public decimal SubTotal { get; set; }

    [Display(Name = "Toplam İndirim")]
    public decimal DiscountTotal { get; set; }

    [Display(Name = "Toplam KDV Tutarı")]
    public decimal VatTotal { get; set; }

    [Display(Name = "Genel Toplam")]
    public decimal GrandTotal { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<InvoiceAttachment> Attachments { get; set; } = new();

    // Sipariş belgeleri için: durum ve dönüştürüldüğü fatura
    public OrderStatus? OrderState { get; set; }
    public int? ConvertedInvoiceId { get; set; }

    public bool IsSales => Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail;
    public bool IsOrder => Type is InvoiceType.SalesOrder or InvoiceType.PurchaseOrder;

    public decimal GrandTotalTry => Math.Round(GrandTotal * ExchangeRate, 2);
    public decimal VatTotalTry => Math.Round(VatTotal * ExchangeRate, 2);
    public decimal StopajTotalTry => Math.Round(StopajTotal * ExchangeRate, 2);
    public decimal TevkifatVatTotalTry => Math.Round(TevkifatVatTotal * ExchangeRate, 2);
    public string CurrencySymbol => Currency switch { "USD" => "$", "EUR" => "€", _ => "₺" };

    // Tahsilat/ödemeler her zaman TL girilir; kalan da TL karşılığı üzerinden izlenir
    public decimal PaidTotal => Payments.Sum(p => p.Amount);
    public decimal RemainingTotal => GrandTotalTry - PaidTotal;
}
