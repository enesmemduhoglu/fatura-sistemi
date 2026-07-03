using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Tests;

/// <summary>
/// SQLite in-memory veritabanı üzerinde gerçek AppDbContext kurar; bağlantı açık
/// kaldığı sürece şema yaşar. Üretimle aynı sağlayıcı kullanıldığı için SQLite'a
/// özgü davranışlar (decimal kısıtları vb.) testlerde de geçerlidir.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>Aynı veritabanına bakan yeni bir context (izleme durumundan bağımsız doğrulama için).</summary>
    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

public static class TestData
{
    public static Firm AddFirm(AppDbContext db, string name = "Test Firma", bool customer = true, bool supplier = true)
    {
        var firm = new Firm { Name = name, IsCustomer = customer, IsSupplier = supplier };
        db.Firms.Add(firm);
        db.SaveChanges();
        return firm;
    }

    public static Safe AddSafe(AppDbContext db, decimal opening = 0)
    {
        var safe = new Safe { Name = "Kasa", OpeningBalance = opening };
        db.Safes.Add(safe);
        db.SaveChanges();
        return safe;
    }

    public static BankAccount AddBank(AppDbContext db, decimal opening = 0)
    {
        var bank = new BankAccount { Name = "Banka", OpeningBalance = opening };
        db.BankAccounts.Add(bank);
        db.SaveChanges();
        return bank;
    }

    public static Product AddProduct(AppDbContext db, string name = "Ürün", decimal stock = 0)
    {
        var product = new Product { Name = name, SalePrice = 100, PurchasePrice = 60, VatRate = 20, StockAmount = stock };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    /// <summary>Tek satırlı, hesaplanmış fatura ekler (tutarlar belge dövizindedir).</summary>
    public static Invoice AddInvoice(AppDbContext db, InvoiceType type, Firm firm,
        decimal unitPrice, decimal quantity = 1, decimal vatRate = 20,
        string currency = "TL", decimal rate = 1, Product? product = null,
        DateTime? date = null, DateTime? due = null, InvoiceStatus status = InvoiceStatus.Open)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = $"TST{Guid.NewGuid():N}"[..16],
            Type = type,
            FirmId = firm.Id,
            InvoiceDate = date ?? DateTime.Today,
            DueDate = due,
            Currency = currency,
            ExchangeRate = rate,
            Status = status,
            OrderState = type is InvoiceType.SalesOrder or InvoiceType.PurchaseOrder ? OrderStatus.Waiting : null,
            Lines = new List<InvoiceLine>
            {
                new()
                {
                    ProductId = product?.Id,
                    ItemName = product?.Name ?? "Kalem",
                    Quantity = quantity, Unit = "Adet",
                    UnitPrice = unitPrice, VatRate = vatRate
                }
            }
        };
        InvoiceCalculator.Calculate(invoice);
        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoice;
    }
}

/// <summary>Controller testlerinde TempData ataması için bellek içi sağlayıcı.</summary>
public sealed class FakeTempDataProvider : ITempDataProvider
{
    private IDictionary<string, object?> _data = new Dictionary<string, object?>();
    public IDictionary<string, object?> LoadTempData(HttpContext context) => _data;
    public void SaveTempData(HttpContext context, IDictionary<string, object?> values) => _data = values;
}

public static class ControllerTestExtensions
{
    public static T WithTempData<T>(this T controller) where T : Controller
    {
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider());
        return controller;
    }
}
