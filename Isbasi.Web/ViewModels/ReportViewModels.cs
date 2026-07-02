namespace Isbasi.Web.ViewModels;

public class ReportMonthRow
{
    public string Month { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal Net { get; set; }     // KDV hariç toplam
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }   // genel toplam
}

public class ReportFirmRow
{
    public string FirmName { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal Gross { get; set; }
}

public class ReportSummaryViewModel
{
    public string Title { get; set; } = "";
    public string FirmColumnTitle { get; set; } = "Firma";
    public int Year { get; set; }
    public List<ReportMonthRow> Months { get; set; } = new();
    public List<ReportFirmRow> Firms { get; set; } = new();

    public decimal TotalNet => Months.Sum(m => m.Net);
    public decimal TotalVat => Months.Sum(m => m.Vat);
    public decimal TotalGross => Months.Sum(m => m.Gross);
    public int TotalCount => Months.Sum(m => m.InvoiceCount);
}

public class VatMonthRow
{
    public string Month { get; set; } = "";
    public decimal CalculatedVat { get; set; }   // hesaplanan KDV (satış)
    public decimal DeductibleVat { get; set; }   // indirilecek KDV (alış + gider)
    public decimal Difference => CalculatedVat - DeductibleVat;
}

public class VatReportViewModel
{
    public int Year { get; set; }
    public List<VatMonthRow> Months { get; set; } = new();
    public decimal TotalCalculated => Months.Sum(m => m.CalculatedVat);
    public decimal TotalDeductible => Months.Sum(m => m.DeductibleVat);
    public decimal TotalDifference => TotalCalculated - TotalDeductible;
}
