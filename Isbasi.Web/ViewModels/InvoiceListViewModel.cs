using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class InvoiceListViewModel
{
    public string Mode { get; set; } = "Sales";   // Sales | Purchase | Expense | Orders | PurchaseOrders | SalesReturns | PurchaseReturns
    public bool IsSales => Mode == "Sales";
    public bool IsExpense => Mode == "Expense";
    public bool IsOrderMode => Mode is "Orders" or "PurchaseOrders";
    public bool IsReturnMode => Mode is "SalesReturns" or "PurchaseReturns";

    // İade dahil para giren taraf: tahsilat sütun başlıkları buna göre seçilir
    public bool IsCashIncoming => IsSales || Mode == "PurchaseReturns";

    public string Title => Mode switch
    {
        "Sales" => "Satış Faturaları",
        "Purchase" => "Alış Faturaları",
        "Orders" => "Siparişler",
        "PurchaseOrders" => "Alış Siparişleri",
        "SalesReturns" => "Satış İade Faturaları",
        "PurchaseReturns" => "Alış İade Faturaları",
        _ => "Giderler"
    };

    public string BaseUrl => Mode switch
    {
        "Sales" => "/invoice/sales",
        "Purchase" => "/invoice/purchase",
        "Orders" => "/invoice/orders",
        "PurchaseOrders" => "/invoice/purchaseorders",
        "SalesReturns" => "/invoice/salesreturns",
        "PurchaseReturns" => "/invoice/purchasereturns",
        _ => "/invoice/purchaseservices"
    };

    public string? Status { get; set; }      // "" | "Open" | "Paid"
    public string? Query { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
}
