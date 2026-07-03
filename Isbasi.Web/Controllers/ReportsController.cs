using System.Globalization;
using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("reports")]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;

    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet("salesreport")]
    public Task<IActionResult> SalesReport(int? year)
        => Summary("Satış Özeti", "Müşteri", year,
            new[] { InvoiceType.SalesWholesale, InvoiceType.SalesRetail });

    [HttpGet("purchasereport")]
    public Task<IActionResult> PurchaseReport(int? year)
        => Summary("Alış Özeti", "Tedarikçi", year,
            new[] { InvoiceType.Purchase });

    [HttpGet("servicereport")]
    public Task<IActionResult> ServiceReport(int? year)
        => Summary("Masraf Özeti", "Tedarikçi", year,
            new[] { InvoiceType.Expense });

    private async Task<IActionResult> Summary(string title, string firmColumn, int? year, InvoiceType[] types)
    {
        int reportYear = year ?? DateTime.Today.Year;
        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .Where(i => types.Contains(i.Type) && i.InvoiceDate.Year == reportYear)
            .ToListAsync();

        var vm = new ReportSummaryViewModel { Title = title, FirmColumnTitle = firmColumn, Year = reportYear };
        var culture = new CultureInfo("tr-TR");

        for (int month = 1; month <= 12; month++)
        {
            var monthInvoices = invoices.Where(i => i.InvoiceDate.Month == month).ToList();
            vm.Months.Add(new ReportMonthRow
            {
                Month = culture.DateTimeFormat.GetMonthName(month),
                InvoiceCount = monthInvoices.Count,
                Net = monthInvoices.Sum(i => i.GrandTotalTry - i.VatTotalTry),
                Vat = monthInvoices.Sum(i => i.VatTotalTry),
                Gross = monthInvoices.Sum(i => i.GrandTotalTry)
            });
        }

        vm.Firms = invoices
            .GroupBy(i => i.Firm?.Name ?? "-")
            .Select(g => new ReportFirmRow
            {
                FirmName = g.Key,
                InvoiceCount = g.Count(),
                Gross = g.Sum(i => i.GrandTotalTry)
            })
            .OrderByDescending(f => f.Gross)
            .ToList();

        return View("Summary", vm);
    }

    [HttpGet("salespurchase")]
    public async Task<IActionResult> SalesPurchase(int? year)
    {
        int reportYear = year ?? DateTime.Today.Year;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == reportYear
                && i.Type != InvoiceType.SalesOrder && i.Type != InvoiceType.PurchaseOrder)
            .Select(i => new { i.InvoiceDate, i.Type, i.GrandTotal, i.ExchangeRate })
            .ToListAsync();

        var vm = new SalesPurchaseViewModel { Year = reportYear };
        var culture = new CultureInfo("tr-TR");

        for (int month = 1; month <= 12; month++)
        {
            var monthInvoices = invoices.Where(i => i.InvoiceDate.Month == month).ToList();
            vm.Months.Add(new SalesPurchaseMonthRow
            {
                Month = culture.DateTimeFormat.GetMonthName(month),
                Sales = monthInvoices
                    .Where(i => i.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail)
                    .Sum(i => Math.Round(i.GrandTotal * i.ExchangeRate, 2)),
                Purchases = monthInvoices
                    .Where(i => i.Type == InvoiceType.Purchase)
                    .Sum(i => Math.Round(i.GrandTotal * i.ExchangeRate, 2)),
                Expenses = monthInvoices
                    .Where(i => i.Type == InvoiceType.Expense)
                    .Sum(i => Math.Round(i.GrandTotal * i.ExchangeRate, 2))
            });
        }

        return View(vm);
    }

    [HttpGet("orderstatus")]
    public async Task<IActionResult> OrderStatus()
    {
        var orders = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .Where(i => i.Type == InvoiceType.SalesOrder || i.Type == InvoiceType.PurchaseOrder)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        var vm = new OrderStatusViewModel { Orders = orders };
        foreach (var (type, title) in new[]
        {
            (InvoiceType.SalesOrder, "Satış Siparişleri"),
            (InvoiceType.PurchaseOrder, "Alış Siparişleri")
        })
        {
            var group = new OrderStatusGroup { Title = title };
            foreach (var (state, label, badge) in new[]
            {
                (Models.OrderStatus.Waiting, "Bekliyor", "status-open"),
                (Models.OrderStatus.Invoiced, "Faturalandı", "status-paid"),
                (Models.OrderStatus.Cancelled, "İptal", "status-cancelled")
            })
            {
                var matching = orders.Where(o => o.Type == type && o.OrderState == state).ToList();
                group.Rows.Add(new OrderStatusRow
                {
                    Status = label,
                    BadgeClass = badge,
                    Count = matching.Count,
                    Total = matching.Sum(o => o.GrandTotalTry)
                });
            }
            vm.Groups.Add(group);
        }

        return View(vm);
    }

    [HttpGet("vatreport")]
    public async Task<IActionResult> VatReport(int? year)
    {
        int reportYear = year ?? DateTime.Today.Year;
        var invoiceRows = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == reportYear)
            .Select(i => new { i.InvoiceDate, i.Type, i.VatTotal, i.ExchangeRate })
            .ToListAsync();
        // Çarpım bellekte: SQLite decimal aritmetiğini SQL'e çeviremez
        var invoices = invoiceRows
            .Select(i => new { i.InvoiceDate, i.Type, VatTotal = Math.Round(i.VatTotal * i.ExchangeRate, 2) })
            .ToList();
        // SMM KDV'leri de beyana girer: verilen makbuz hesaplanan, alınan indirilecek
        var receipts = await _db.FreelanceReceipts.AsNoTracking()
            .Where(r => r.Date.Year == reportYear)
            .Select(r => new { r.Date, r.Type, r.GrossAmount, r.VatRate })
            .ToListAsync();

        var vm = new VatReportViewModel { Year = reportYear };
        var culture = new CultureInfo("tr-TR");

        for (int month = 1; month <= 12; month++)
        {
            var monthInvoices = invoices.Where(i => i.InvoiceDate.Month == month).ToList();
            var monthReceipts = receipts.Where(r => r.Date.Month == month).ToList();
            vm.Months.Add(new VatMonthRow
            {
                Month = culture.DateTimeFormat.GetMonthName(month),
                CalculatedVat = monthInvoices
                    .Where(i => i.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail)
                    .Sum(i => i.VatTotal)
                    + monthReceipts.Where(r => r.Type == ReceiptType.Issued)
                        .Sum(r => Math.Round(r.GrossAmount * r.VatRate / 100m, 2)),
                DeductibleVat = monthInvoices
                    .Where(i => i.Type is InvoiceType.Purchase or InvoiceType.Expense)
                    .Sum(i => i.VatTotal)
                    + monthReceipts.Where(r => r.Type == ReceiptType.Received)
                        .Sum(r => Math.Round(r.GrossAmount * r.VatRate / 100m, 2))
            });
        }

        return View(vm);
    }
}
