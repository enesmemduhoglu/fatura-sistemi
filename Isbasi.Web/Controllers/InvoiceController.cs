using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Isbasi.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Web.Controllers;

[Route("invoice")]
public class InvoiceController : Controller
{
    private readonly AppDbContext _db;

    public InvoiceController(AppDbContext db) => _db = db;

    [HttpGet("sales")]
    public Task<IActionResult> Sales(string? status, string? q, DateTime? start, DateTime? end)
        => List("Sales", status, q, start, end);

    [HttpGet("purchase")]
    public Task<IActionResult> Purchase(string? status, string? q, DateTime? start, DateTime? end)
        => List("Purchase", status, q, start, end);

    [HttpGet("purchaseservices")]
    public Task<IActionResult> Expenses(string? status, string? q, DateTime? start, DateTime? end)
        => List("Expense", status, q, start, end);

    [HttpGet("orders")]
    public Task<IActionResult> Orders(string? status, string? q, DateTime? start, DateTime? end)
        => List("Orders", status, q, start, end);

    [HttpGet("purchaseorders")]
    public Task<IActionResult> PurchaseOrders(string? status, string? q, DateTime? start, DateTime? end)
        => List("PurchaseOrders", status, q, start, end);

    private async Task<IActionResult> List(string mode, string? status, string? q, DateTime? start, DateTime? end)
    {
        var query = _db.Invoices.AsNoTracking().Include(i => i.Firm).Include(i => i.Payments).AsQueryable();
        query = mode switch
        {
            "Sales" => query.Where(i => i.Type == InvoiceType.SalesWholesale || i.Type == InvoiceType.SalesRetail),
            "Purchase" => query.Where(i => i.Type == InvoiceType.Purchase),
            "Orders" => query.Where(i => i.Type == InvoiceType.SalesOrder),
            "PurchaseOrders" => query.Where(i => i.Type == InvoiceType.PurchaseOrder),
            _ => query.Where(i => i.Type == InvoiceType.Expense)
        };

        // Siparişlerde durum filtresi sipariş durumudur, faturalarda ödeme durumu
        if (status == "Open") query = query.Where(i => i.Status == InvoiceStatus.Open);
        else if (status == "Paid") query = query.Where(i => i.Status == InvoiceStatus.Paid);
        else if (status == "Waiting") query = query.Where(i => i.OrderState == OrderStatus.Waiting);
        else if (status == "Invoiced") query = query.Where(i => i.OrderState == OrderStatus.Invoiced);
        else if (status == "Cancelled") query = query.Where(i => i.OrderState == OrderStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Firm!.Name.Contains(q) || i.InvoiceNumber.Contains(q));

        if (start.HasValue) query = query.Where(i => i.InvoiceDate >= start.Value);
        if (end.HasValue) query = query.Where(i => i.InvoiceDate < end.Value.AddDays(1));

        var vm = new InvoiceListViewModel
        {
            Mode = mode,
            Status = status,
            Query = q,
            StartDate = start,
            EndDate = end,
            Invoices = await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync()
        };
        return View("List", vm);
    }

    // type: Gross = Toptan (KDV Hariç), Net = Perakende (KDV Dahil)
    [HttpGet("sales/edit")]
    public Task<IActionResult> EditSales(int? id, string type = "Gross")
        => Edit(id, type == "Net" ? InvoiceType.SalesRetail : InvoiceType.SalesWholesale);

    [HttpGet("purchase/edit")]
    public Task<IActionResult> EditPurchase(int? id)
        => Edit(id, InvoiceType.Purchase);

    [HttpGet("purchaseservices/edit")]
    public Task<IActionResult> EditExpense(int? id)
        => Edit(id, InvoiceType.Expense);

    [HttpGet("orders/edit")]
    public Task<IActionResult> EditOrder(int? id)
        => Edit(id, InvoiceType.SalesOrder);

    [HttpGet("purchaseorders/edit")]
    public Task<IActionResult> EditPurchaseOrder(int? id)
        => Edit(id, InvoiceType.PurchaseOrder);

    private async Task<IActionResult> Edit(int? id, InvoiceType type)
    {
        Invoice invoice;
        if (id.HasValue)
        {
            var existing = await _db.Invoices.AsNoTracking()
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == id.Value);
            if (existing == null) return NotFound();
            invoice = existing;
        }
        else
        {
            invoice = new Invoice { Type = type, InvoiceDate = DateTime.Now };
            if (invoice.IsOrder) invoice.OrderState = OrderStatus.Waiting;
        }

        await FillEditViewBags(invoice);
        return View("Edit", invoice);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Invoice model, string? submitAction,
        string? firmAddress, string? firmCountry, string? firmCity, string? firmDistrict,
        string? firmTaxOffice, string? firmTaxNumber, FirmKind? firmKind)
    {
        model.Lines = model.Lines.Where(l => !string.IsNullOrWhiteSpace(l.ItemName)).ToList();

        if (model.FirmId == 0)
            ModelState.AddModelError("FirmId", "Firma seçimi zorunludur.");
        if (model.Lines.Count == 0)
            ModelState.AddModelError("Lines", "En az bir fatura satırı ekleyin.");

        if (!ModelState.IsValid)
        {
            await FillEditViewBags(model);
            return View("Edit", model);
        }

        InvoiceCalculator.Calculate(model);

        // Formda düzenlenen firma bilgileri seçili firmanın kartına yazılır (İşbaşı davranışı)
        var firm = await _db.Firms.FindAsync(model.FirmId);
        if (firm != null)
        {
            firm.Address = string.IsNullOrWhiteSpace(firmAddress) ? null : firmAddress.Trim();
            firm.Country = string.IsNullOrWhiteSpace(firmCountry) ? null : firmCountry.Trim();
            firm.City = string.IsNullOrWhiteSpace(firmCity) ? null : firmCity.Trim();
            firm.District = string.IsNullOrWhiteSpace(firmDistrict) ? null : firmDistrict.Trim();
            firm.TaxOffice = string.IsNullOrWhiteSpace(firmTaxOffice) ? null : firmTaxOffice.Trim();
            firm.TaxNumber = string.IsNullOrWhiteSpace(firmTaxNumber) ? null : firmTaxNumber.Trim();
            if (firmKind.HasValue) firm.Kind = firmKind.Value;
        }

        if (model.IsOrder && model.OrderState == null) model.OrderState = OrderStatus.Waiting;

        if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            model.InvoiceNumber = await NextInvoiceNumber(model.IsOrder ? "SIP" : "ISB");

        if (model.Id == 0)
        {
            _db.Invoices.Add(model);
        }
        else
        {
            var existingLineIds = model.Lines.Where(l => l.Id != 0).Select(l => l.Id).ToList();
            var removed = _db.InvoiceLines.Where(l => l.InvoiceId == model.Id && !existingLineIds.Contains(l.Id));
            _db.InvoiceLines.RemoveRange(removed);

            _db.Update(model);
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = "Fatura kaydedildi.";

        if (submitAction == "saveAndNew")
        {
            return model.Type switch
            {
                InvoiceType.Purchase => RedirectToAction(nameof(EditPurchase)),
                InvoiceType.Expense => RedirectToAction(nameof(EditExpense)),
                InvoiceType.SalesOrder => RedirectToAction(nameof(EditOrder)),
                InvoiceType.PurchaseOrder => RedirectToAction(nameof(EditPurchaseOrder)),
                _ => RedirectToAction(nameof(EditSales), new { type = model.Type == InvoiceType.SalesRetail ? "Net" : "Gross" })
            };
        }
        return RedirectToList(model.Type);
    }

    [HttpGet("print/{id:int}")]
    public async Task<IActionResult> Print(int id)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Firm)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice == null) return NotFound();

        ViewBag.Company = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        return View(invoice);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(string mode = "Sales", string? status = null, string? q = null,
        DateTime? start = null, DateTime? end = null)
    {
        var query = _db.Invoices.AsNoTracking().Include(i => i.Firm).Include(i => i.Payments).AsQueryable();
        query = mode switch
        {
            "Sales" => query.Where(i => i.Type == InvoiceType.SalesWholesale || i.Type == InvoiceType.SalesRetail),
            "Purchase" => query.Where(i => i.Type == InvoiceType.Purchase),
            "Orders" => query.Where(i => i.Type == InvoiceType.SalesOrder),
            "PurchaseOrders" => query.Where(i => i.Type == InvoiceType.PurchaseOrder),
            _ => query.Where(i => i.Type == InvoiceType.Expense)
        };
        if (status == "Open") query = query.Where(i => i.Status == InvoiceStatus.Open);
        else if (status == "Paid") query = query.Where(i => i.Status == InvoiceStatus.Paid);
        else if (status == "Waiting") query = query.Where(i => i.OrderState == OrderStatus.Waiting);
        else if (status == "Invoiced") query = query.Where(i => i.OrderState == OrderStatus.Invoiced);
        else if (status == "Cancelled") query = query.Where(i => i.OrderState == OrderStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Firm!.Name.Contains(q) || i.InvoiceNumber.Contains(q));
        if (start.HasValue) query = query.Where(i => i.InvoiceDate >= start.Value);
        if (end.HasValue) query = query.Where(i => i.InvoiceDate < end.Value.AddDays(1));

        var invoices = await query.OrderByDescending(i => i.InvoiceDate).ToListAsync();
        bool isOrderMode = mode is "Orders" or "PurchaseOrders";

        // Türkçe Excel: noktalı virgül ayırıcı + UTF-8 BOM
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(isOrderMode
            ? "Sipariş No;Firma;Sipariş Tipi;Tarih;Teslim Tarihi;Matrah;KDV;Genel Toplam;Durum"
            : "Fatura No;Firma;Fatura Tipi;Fatura Tarihi;Vade Tarihi;Matrah;KDV;Genel Toplam;Tahsil Edilen;Durum");
        foreach (var i in invoices)
        {
            string typeName = i.Type switch
            {
                InvoiceType.SalesWholesale => "Toptan (KDV Hariç)",
                InvoiceType.SalesRetail => "Perakende (KDV Dahil)",
                InvoiceType.Expense => "Gider",
                InvoiceType.SalesOrder => "Satış Siparişi",
                InvoiceType.PurchaseOrder => "Alış Siparişi",
                _ => "Alış"
            };
            string firmName = (i.Firm?.Name ?? "").Replace(";", ",");
            if (isOrderMode)
            {
                string orderStatus = i.OrderState switch
                {
                    OrderStatus.Invoiced => "Faturalandı",
                    OrderStatus.Cancelled => "İptal",
                    _ => "Bekliyor"
                };
                sb.AppendLine($"{i.InvoiceNumber};{firmName};{typeName};{i.InvoiceDate:dd.MM.yyyy};" +
                    $"{i.DueDate:dd.MM.yyyy};{i.GrandTotal - i.VatTotal:N2};{i.VatTotal:N2};{i.GrandTotal:N2};{orderStatus}");
            }
            else
            {
                sb.AppendLine($"{i.InvoiceNumber};{firmName};{typeName};{i.InvoiceDate:dd.MM.yyyy};" +
                    $"{i.DueDate:dd.MM.yyyy};{i.GrandTotal - i.VatTotal:N2};{i.VatTotal:N2};{i.GrandTotal:N2};" +
                    $"{i.PaidTotal:N2};{(i.Status == InvoiceStatus.Paid ? "Ödendi" : "Açık")}");
            }
        }

        string fileName = mode switch
        {
            "Sales" => "satis-faturalari",
            "Purchase" => "alis-faturalari",
            "Orders" => "satis-siparisleri",
            "PurchaseOrders" => "alis-siparisleri",
            _ => "giderler"
        };
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"{fileName}-{DateTime.Today:yyyyMMdd}.csv");
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices.FindAsync(id);
        if (invoice == null) return NotFound();

        var type = invoice.Type;
        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Fatura silindi.";
        return RedirectToList(type);
    }

    private IActionResult RedirectToList(InvoiceType type) => type switch
    {
        InvoiceType.Purchase => RedirectToAction(nameof(Purchase)),
        InvoiceType.Expense => RedirectToAction(nameof(Expenses)),
        InvoiceType.SalesOrder => RedirectToAction(nameof(Orders)),
        InvoiceType.PurchaseOrder => RedirectToAction(nameof(PurchaseOrders)),
        _ => RedirectToAction(nameof(Sales))
    };

    // Sipariş: bekleyen siparişi faturaya kopyalar ve Faturalandı işaretler
    [HttpPost("convert/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id)
    {
        var order = await _db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        if (order == null) return NotFound();
        if (!order.IsOrder || order.OrderState != OrderStatus.Waiting)
        {
            TempData["Error"] = "Yalnızca bekleyen siparişler faturaya dönüştürülebilir.";
            return RedirectToList(order.Type);
        }

        var invoice = new Invoice
        {
            Type = order.Type == InvoiceType.SalesOrder ? InvoiceType.SalesWholesale : InvoiceType.Purchase,
            FirmId = order.FirmId,
            InvoiceDate = DateTime.Now,
            DueDate = order.DueDate,
            Currency = order.Currency,
            Category = order.Category,
            Description = $"{order.InvoiceNumber} numaralı siparişten oluşturuldu. {order.Description}".Trim(),
            GeneralDiscountValue = order.GeneralDiscountValue,
            GeneralDiscountType = order.GeneralDiscountType,
            Lines = order.Lines.Select(l => new InvoiceLine
            {
                ProductId = l.ProductId,
                ServiceId = l.ServiceId,
                ItemName = l.ItemName,
                Quantity = l.Quantity,
                Unit = l.Unit,
                UnitPrice = l.UnitPrice,
                VatRate = l.VatRate,
                DiscountValue = l.DiscountValue,
                DiscountType = l.DiscountType
            }).ToList()
        };
        InvoiceCalculator.Calculate(invoice);
        invoice.InvoiceNumber = await NextInvoiceNumber("ISB");

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        order.OrderState = OrderStatus.Invoiced;
        order.ConvertedInvoiceId = invoice.Id;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Sipariş {invoice.InvoiceNumber} numaralı faturaya dönüştürüldü.";
        return invoice.Type == InvoiceType.Purchase
            ? RedirectToAction(nameof(EditPurchase), new { id = invoice.Id })
            : RedirectToAction(nameof(EditSales), new { id = invoice.Id });
    }

    // Bekleyen siparişi iptal eder / iptal siparişi tekrar bekleyene alır
    [HttpPost("cancelorder/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var order = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (order == null || !order.IsOrder) return NotFound();

        if (order.OrderState == OrderStatus.Waiting)
        {
            order.OrderState = OrderStatus.Cancelled;
            TempData["Success"] = "Sipariş iptal edildi.";
        }
        else if (order.OrderState == OrderStatus.Cancelled)
        {
            order.OrderState = OrderStatus.Waiting;
            TempData["Success"] = "Sipariş tekrar beklemeye alındı.";
        }
        else
        {
            TempData["Error"] = "Faturalanmış sipariş iptal edilemez.";
        }
        await _db.SaveChangesAsync();
        return RedirectToList(order.Type);
    }

    private async Task<string> NextInvoiceNumber(string series = "ISB")
    {
        var year = DateTime.Today.Year;
        var prefix = $"{series}{year}";
        var last = await _db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (last != null && int.TryParse(last.Substring(prefix.Length), out int lastSeq))
            seq = lastSeq + 1;
        return $"{prefix}{seq:D9}";
    }

    private async Task FillEditViewBags(Invoice invoice)
    {
        // Satış tarafı (satış faturaları + satış siparişi) müşterileri ve satış fiyatını kullanır
        bool isSales = invoice.IsSales || invoice.Type == InvoiceType.SalesOrder;
        bool isExpense = invoice.Type == InvoiceType.Expense;

        ViewBag.Firms = await _db.Firms.AsNoTracking()
            .Where(f => isSales ? f.IsCustomer : f.IsSupplier)
            .OrderBy(f => f.Name)
            .ToListAsync();

        // select2 + otomatik doldurma için ürün/hizmet kataloğu (giderlerde sadece hizmet/masraf)
        var services = await _db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        var items = services.Select(s => new
        {
            key = $"s{s.Id}",
            productId = (int?)null,
            serviceId = (int?)s.Id,
            name = s.Name,
            unit = s.Unit,
            price = s.Price,
            vatRate = s.VatRate
        }).ToList();

        if (!isExpense)
        {
            var products = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
            items = products.Select(p => new
            {
                key = $"p{p.Id}",
                productId = (int?)p.Id,
                serviceId = (int?)null,
                name = p.Name,
                unit = p.Unit,
                price = isSales ? p.SalePrice : p.PurchasePrice,
                vatRate = p.VatRate
            }).Concat(items).ToList();
        }
        ViewBag.Items = items;
    }
}
