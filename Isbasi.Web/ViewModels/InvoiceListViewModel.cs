using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class InvoiceListViewModel
{
    public string Mode { get; set; } = "Sales";   // Sales | Purchase | Expense
    public bool IsSales => Mode == "Sales";
    public bool IsExpense => Mode == "Expense";

    public string Title => Mode switch
    {
        "Sales" => "Satış Faturaları",
        "Purchase" => "Alış Faturaları",
        _ => "Giderler"
    };

    public string BaseUrl => Mode switch
    {
        "Sales" => "/invoice/sales",
        "Purchase" => "/invoice/purchase",
        _ => "/invoice/purchaseservices"
    };

    public string? Status { get; set; }      // "" | "Open" | "Paid"
    public string? Query { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
}
