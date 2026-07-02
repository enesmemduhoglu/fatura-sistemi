namespace Isbasi.Web.ViewModels;

public class DashboardViewModel
{
    public int Year { get; set; }
    public decimal[] MonthlyIncome { get; set; } = new decimal[12];
    public decimal[] MonthlyPayment { get; set; } = new decimal[12];

    public decimal Receivables { get; set; }   // tahsil edilecek (açık satış faturaları)
    public decimal Payables { get; set; }      // ödenecek (açık alış faturaları)

    public decimal SalesLastMonth { get; set; }
    public decimal SalesThisMonth { get; set; }

    // Satışlarım ve Tahsilatlarım grafiği: etiketler + değerler (JSON'a çevrilir)
    public List<string> ChartLabelsWeek { get; set; } = new();
    public List<decimal> ChartSalesWeek { get; set; } = new();
    public List<string> ChartLabelsMonth { get; set; } = new();
    public List<decimal> ChartSalesMonth { get; set; } = new();
    public List<string> ChartLabelsYear { get; set; } = new();
    public List<decimal> ChartSalesYear { get; set; } = new();
}
