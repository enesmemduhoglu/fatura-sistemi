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
        // Vadesi gelen tekrarlama planlarından faturalar üretilir (lazy tetikleme)
        int generated = await RecurringGenerator.GenerateDue(_db);
        if (generated > 0)
            TempData["Success"] = $"{generated} tekrarlayan fatura otomatik oluşturuldu.";

        var year = DateTime.Today.Year;
        // Siparişler mali kayıt değildir; nakit akışı ve bilanço dışında tutulur
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate.Year == year
                && i.Type != InvoiceType.SalesOrder && i.Type != InvoiceType.PurchaseOrder)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            Year = year,
            Safes = await _db.Safes.AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
            Banks = await _db.BankAccounts.AsNoTracking().OrderBy(b => b.Name).ToListAsync(),
            CashBalances = await CashBalanceCalculator.Compute(_db)
        };

        // Tahsilatlar: satış faturaları ve alış iadelerine girilen tahsilatlar
        var collections = await _db.Payments.AsNoTracking()
            .Where(p => p.Invoice!.Type == InvoiceType.SalesWholesale
                || p.Invoice.Type == InvoiceType.SalesRetail
                || p.Invoice.Type == InvoiceType.PurchaseReturn)
            .Select(p => new { p.Date, p.Amount })
            .ToListAsync();

        // Dövizli belgeler TL karşılığıyla toplanır; iadeler kendi tarafından düşülür
        foreach (var inv in invoices)
        {
            int m = inv.InvoiceDate.Month - 1;
            if (inv.IsSales) vm.MonthlyIncome[m] += inv.GrandTotalTry;
            else if (inv.Type == InvoiceType.SalesReturn) vm.MonthlyIncome[m] -= inv.GrandTotalTry;
            else if (inv.Type == InvoiceType.PurchaseReturn) vm.MonthlyPayment[m] -= inv.GrandTotalTry;
            else vm.MonthlyPayment[m] += inv.GrandTotalTry;
        }

        // Açık iadelerde para yönü tersinedir: alış iadesi alacak, satış iadesi borç doğurur
        vm.Receivables = invoices.Where(i => i.IsCashIncoming && i.Status == InvoiceStatus.Open).Sum(i => i.GrandTotalTry);
        vm.Payables = invoices.Where(i => !i.IsCashIncoming && i.Status == InvoiceStatus.Open).Sum(i => i.GrandTotalTry);

        // Portföydeki çekler mini bilanço ve ÇEK panelinde gösterilir
        var portfolioCheques = await _db.Cheques.AsNoTracking()
            .Where(c => c.Status == ChequeStatus.Portfolio)
            .Select(c => new { c.Type, c.Amount })
            .ToListAsync();
        vm.ChequesReceivable = portfolioCheques.Where(c => c.Type == ChequeType.Received).Sum(c => c.Amount);
        vm.ChequesPayable = portfolioCheques.Where(c => c.Type == ChequeType.Issued).Sum(c => c.Amount);

        var today = DateTime.Today;

        // Vadesi geçen açık faturalar (yıldan bağımsız)
        var overdue = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .Where(i => i.Status == InvoiceStatus.Open
                && i.Type != InvoiceType.SalesOrder && i.Type != InvoiceType.PurchaseOrder
                && i.DueDate != null && i.DueDate < today)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
        vm.OverdueSales = overdue.Where(i => i.IsCashIncoming).ToList();
        vm.OverduePurchases = overdue.Where(i => !i.IsCashIncoming).ToList();

        // Satış grafikleri net satışı gösterir: satış iadeleri negatif tutarla katılır
        var sales = invoices
            .Where(i => i.IsSales || i.Type == InvoiceType.SalesReturn)
            .Select(i => new
            {
                i.InvoiceDate,
                Amount = i.Type == InvoiceType.SalesReturn ? -i.GrandTotalTry : i.GrandTotalTry
            })
            .ToList();

        vm.SalesThisMonth = sales.Where(i => i.InvoiceDate.Month == today.Month).Sum(i => i.Amount);
        vm.SalesLastMonth = sales.Where(i => i.InvoiceDate >= today.AddDays(-30)).Sum(i => i.Amount);
        vm.CollectionsThisMonth = collections.Where(c => c.Date.Year == today.Year && c.Date.Month == today.Month).Sum(c => c.Amount);
        vm.CollectionsLastMonth = collections.Where(c => c.Date >= today.AddDays(-30)).Sum(c => c.Amount);

        // Son 1 hafta: günlük
        for (int d = 6; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            vm.ChartLabelsWeek.Add(day.ToString("dd MMM"));
            vm.ChartSalesWeek.Add(sales.Where(i => i.InvoiceDate.Date == day).Sum(i => i.Amount));
            vm.ChartCollectionsWeek.Add(collections.Where(c => c.Date.Date == day).Sum(c => c.Amount));
        }
        // Son 1 ay: haftalık
        for (int w = 3; w >= 0; w--)
        {
            var start = today.AddDays(-(w + 1) * 7 + 1);
            var end = today.AddDays(-w * 7);
            vm.ChartLabelsMonth.Add($"{start:dd MMM} - {end:dd MMM}");
            vm.ChartSalesMonth.Add(sales.Where(i => i.InvoiceDate.Date >= start && i.InvoiceDate.Date <= end).Sum(i => i.Amount));
            vm.ChartCollectionsMonth.Add(collections.Where(c => c.Date.Date >= start && c.Date.Date <= end).Sum(c => c.Amount));
        }
        // Son 1 yıl: aylık
        for (int mo = 11; mo >= 0; mo--)
        {
            var month = new DateTime(today.Year, today.Month, 1).AddMonths(-mo);
            vm.ChartLabelsYear.Add(month.ToString("MMM yy"));
            vm.ChartSalesYear.Add(sales.Where(i => i.InvoiceDate.Year == month.Year && i.InvoiceDate.Month == month.Month).Sum(i => i.Amount));
            vm.ChartCollectionsYear.Add(collections.Where(c => c.Date.Year == month.Year && c.Date.Month == month.Month).Sum(c => c.Amount));
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
