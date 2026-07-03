using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class InvoiceListViewModel
{
    public string Mode { get; set; } = "Sales";   // Sales | Purchase | Expense | Orders | PurchaseOrders
    public bool IsSales => Mode == "Sales";
    public bool IsExpense => Mode == "Expense";
    public bool IsOrderMode => Mode is "Orders" or "PurchaseOrders";

    public string Title => Mode switch
    {
        "Sales" => "Satış Faturaları",
        "Purchase" => "Alış Faturaları",
        "Orders" => "Siparişler",
        "PurchaseOrders" => "Alış Siparişleri",
        _ => "Giderler"
    };

    public string BaseUrl => Mode switch
    {
        "Sales" => "/invoice/sales",
        "Purchase" => "/invoice/purchase",
        "Orders" => "/invoice/orders",
        "PurchaseOrders" => "/invoice/purchaseorders",
        _ => "/invoice/purchaseservices"
    };

    public string? Status { get; set; }      // "" | "Open" | "Paid"
    public string? Query { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
}
