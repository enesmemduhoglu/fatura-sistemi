using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var year = DateTime.Today.Year;
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == year)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            Year = year,
            Safes = await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            Banks = await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync()
        };

        foreach (var inv in invoices)
        {
            int m = inv.InvoiceDate.Month - 1;
            if (inv.IsSales) vm.MonthlyIncome[m] += inv.GrandTotal;
            else vm.MonthlyPayment[m] += inv.GrandTotal;
        }

        vm.Receivables = invoices.Where(i => i.IsSales && i.Status == InvoiceStatus.Open).Sum(i => i.GrandTotal);
        vm.Payables = invoices.Where(i => !i.IsSales && i.Status == InvoiceStatus.Open).Sum(i => i.GrandTotal);

        var today = DateTime.Today;
        var sales = invoices.Where(i => i.IsSales).ToList();

        vm.SalesThisMonth = sales.Where(i => i.InvoiceDate.Month == today.Month).Sum(i => i.GrandTotal);
        vm.SalesLastMonth = sales.Where(i => i.InvoiceDate >= today.AddDays(-30)).Sum(i => i.GrandTotal);

        // Son 1 hafta: günlük
        for (int d = 6; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            vm.ChartLabelsWeek.Add(day.ToString("dd MMM"));
            vm.ChartSalesWeek.Add(sales.Where(i => i.InvoiceDate.Date == day).Sum(i => i.GrandTotal));
        }
        // Son 1 ay: haftalık
        for (int w = 3; w >= 0; w--)
        {
            var start = today.AddDays(-(w + 1) * 7 + 1);
            var end = today.AddDays(-w * 7);
            vm.ChartLabelsMonth.Add($"{start:dd MMM} - {end:dd MMM}");
            vm.ChartSalesMonth.Add(sales.Where(i => i.InvoiceDate.Date >= start && i.InvoiceDate.Date <= end).Sum(i => i.GrandTotal));
        }
        // Son 1 yıl: aylık
        for (int mo = 11; mo >= 0; mo--)
        {
            var month = new DateTime(today.Year, today.Month, 1).AddMonths(-mo);
            vm.ChartLabelsYear.Add(month.ToString("MMM yy"));
            vm.ChartSalesYear.Add(sales.Where(i => i.InvoiceDate.Year == month.Year && i.InvoiceDate.Month == month.Month).Sum(i => i.GrandTotal));
        }

        return View(vm);
    }

    [Route("comingsoon")]
    public IActionResult ComingSoon(string? title)
    {
        ViewBag.Title = title ?? "Bu Modül";
        return View();
    }

    public IActionResult Error() => View();
}
