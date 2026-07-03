using System.ComponentModel.DataAnnotations;

namespace Isbasi.Web.Models;

public enum RecurringFrequency
{
    [Display(Name = "Haftalık")] Weekly = 0,
    [Display(Name = "Aylık")] Monthly = 1,
    [Display(Name = "3 Aylık")] Quarterly = 2,
    [Display(Name = "Yıllık")] Yearly = 3
}

/// <summary>
/// Tekrarlayan fatura planı: kaynak belge şablon alınır, vadesi gelen her dönemde
/// satırlarıyla yeni bir Açık fatura üretilir (RecurringGenerator).
/// </summary>
public class RecurringPlan
{
    public int Id { get; set; }

    public int SourceInvoiceId { get; set; }
    public Invoice? SourceInvoice { get; set; }

    [Display(Name = "Sıklık")]
    public RecurringFrequency Frequency { get; set; } = RecurringFrequency.Monthly;

    [Display(Name = "İlk Fatura Tarihi")]
    public DateTime NextRunDate { get; set; } = DateTime.Today;

    [Display(Name = "Bitiş Tarihi")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Son Üretim")]
    public DateTime? LastRunDate { get; set; }

    [Display(Name = "Üretilen Fatura")]
    public int GeneratedCount { get; set; }

    public DateTime Advance(DateTime date) => Frequency switch
    {
        RecurringFrequency.Weekly => date.AddDays(7),
        RecurringFrequency.Quarterly => date.AddMonths(3),
        RecurringFrequency.Yearly => date.AddYears(1),
        _ => date.AddMonths(1)
    };
}
