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

public class SalesPurchaseMonthRow
{
    public string Month { get; set; } = "";
    public decimal Sales { get; set; }           // satış faturaları genel toplamı
    public decimal SalesReturns { get; set; }    // satış iade faturaları genel toplamı
    public decimal Purchases { get; set; }       // alış faturaları genel toplamı
    public decimal PurchaseReturns { get; set; } // alış iade faturaları genel toplamı
    public decimal Expenses { get; set; }        // gider faturaları genel toplamı
    public decimal Difference => (Sales - SalesReturns) - (Purchases - PurchaseReturns) - Expenses;
}

public class SalesPurchaseViewModel
{
    public int Year { get; set; }
    public List<SalesPurchaseMonthRow> Months { get; set; } = new();
    public decimal TotalSales => Months.Sum(m => m.Sales);
    public decimal TotalSalesReturns => Months.Sum(m => m.SalesReturns);
    public decimal TotalPurchases => Months.Sum(m => m.Purchases);
    public decimal TotalPurchaseReturns => Months.Sum(m => m.PurchaseReturns);
    public decimal TotalExpenses => Months.Sum(m => m.Expenses);
    public decimal TotalDifference => Months.Sum(m => m.Difference);
}

public class OrderStatusRow
{
    public string Status { get; set; } = "";
    public string BadgeClass { get; set; } = "status-open";
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class OrderStatusGroup
{
    public string Title { get; set; } = "";
    public List<OrderStatusRow> Rows { get; set; } = new();
    public int TotalCount => Rows.Sum(r => r.Count);
    public decimal TotalAmount => Rows.Sum(r => r.Total);
}

public class OrderStatusViewModel
{
    public List<OrderStatusGroup> Groups { get; set; } = new();
    public List<Isbasi.Web.Models.Invoice> Orders { get; set; } = new();
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
