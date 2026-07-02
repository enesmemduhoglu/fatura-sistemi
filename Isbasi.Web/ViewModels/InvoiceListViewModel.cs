using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class InvoiceListViewModel
{
    public bool IsSales { get; set; }
    public string? Status { get; set; }      // "" | "Open" | "Paid"
    public string? Query { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<Invoice> Invoices { get; set; } = new();
}
