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
                Net = monthInvoices.Sum(i => i.GrandTotal - i.VatTotal),
                Vat = monthInvoices.Sum(i => i.VatTotal),
                Gross = monthInvoices.Sum(i => i.GrandTotal)
            });
        }

        vm.Firms = invoices
            .GroupBy(i => i.Firm?.Name ?? "-")
            .Select(g => new ReportFirmRow
            {
                FirmName = g.Key,
                InvoiceCount = g.Count(),
                Gross = g.Sum(i => i.GrandTotal)
            })
            .OrderByDescending(f => f.Gross)
            .ToList();

        return View("Summary", vm);
    }

    [HttpGet("vatreport")]
    public async Task<IActionResult> VatReport(int? year)
    {
        int reportYear = year ?? DateTime.Today.Year;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == reportYear)
            .Select(i => new { i.InvoiceDate, i.Type, i.VatTotal })
            .ToListAsync();

        var vm = new VatReportViewModel { Year = reportYear };
        var culture = new CultureInfo("tr-TR");

        for (int month = 1; month <= 12; month++)
        {
            var monthInvoices = invoices.Where(i => i.InvoiceDate.Month == month).ToList();
            vm.Months.Add(new VatMonthRow
            {
                Month = culture.DateTimeFormat.GetMonthName(month),
                CalculatedVat = monthInvoices
                    .Where(i => i.Type is InvoiceType.SalesWholesale or InvoiceType.SalesRetail)
                    .Sum(i => i.VatTotal),
                DeductibleVat = monthInvoices
                    .Where(i => i.Type is InvoiceType.Purchase or InvoiceType.Expense)
                    .Sum(i => i.VatTotal)
            });
        }

        return View(vm);
    }
}
