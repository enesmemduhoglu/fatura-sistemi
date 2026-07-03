using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class DashboardViewModel
{
    public List<Safe> Safes { get; set; } = new();
    public List<BankAccount> Banks { get; set; } = new();
    public Data.CashBalances? CashBalances { get; set; }
    public decimal SafeBalanceOf(Safe safe) => CashBalances?.SafeBalanceOf(safe) ?? safe.OpeningBalance;
    public decimal BankBalanceOf(BankAccount bank) => CashBalances?.BankBalanceOf(bank) ?? bank.OpeningBalance;
    public decimal SafeTotal => Safes.Sum(SafeBalanceOf);
    public decimal BankTotal => Banks.Sum(BankBalanceOf);
    public decimal TotalCash => SafeTotal + BankTotal;

    public decimal CollectionsLastMonth { get; set; }
    public decimal CollectionsThisMonth { get; set; }
    public List<decimal> ChartCollectionsWeek { get; set; } = new();
    public List<decimal> ChartCollectionsMonth { get; set; } = new();
    public List<decimal> ChartCollectionsYear { get; set; } = new();

    public int Year { get; set; }
    public decimal[] MonthlyIncome { get; set; } = new decimal[12];
    public decimal[] MonthlyPayment { get; set; } = new decimal[12];

    public decimal Receivables { get; set; }   // tahsil edilecek (açık satış faturaları)
    public decimal Payables { get; set; }      // ödenecek (açık alış faturaları)

    // Portföydeki çekler: alınan = tahsil edilecek, verilen = ödenecek
    public decimal ChequesReceivable { get; set; }
    public decimal ChequesPayable { get; set; }

    // Vadesi geçen açık faturalar
    public List<Invoice> OverdueSales { get; set; } = new();
    public List<Invoice> OverduePurchases { get; set; } = new();
    public decimal OverdueSalesTotal => OverdueSales.Sum(i => i.GrandTotalTry);
    public decimal OverduePurchasesTotal => OverduePurchases.Sum(i => i.GrandTotalTry);

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
