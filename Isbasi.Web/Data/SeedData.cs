using Isbasi.Web.Models;

namespace Isbasi.Web.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        if (db.Firms.Any()) return;

        var firms = new List<Firm>
        {
            new() { Name = "Anadolu Ticaret A.Ş.", Kind = FirmKind.Corporate, City = "İstanbul", District = "Kadıköy",
                    Address = "Bağdat Cad. No:120", TaxOffice = "Kadıköy", TaxNumber = "1234567890",
                    Email = "info@anadoluticaret.com", Phone = "0216 123 45 67", IsCustomer = true },
            new() { Name = "Yılmaz Market", Kind = FirmKind.Corporate, City = "Ankara", District = "Çankaya",
                    Address = "Atatürk Bulvarı No:45", TaxOffice = "Çankaya", TaxNumber = "9876543210",
                    Email = "yilmaz@market.com", Phone = "0312 987 65 43", IsCustomer = true },
            new() { Name = "Mehmet Demir", Kind = FirmKind.Individual, City = "İzmir", District = "Konak",
                    TaxNumber = "12345678901", IsCustomer = true },
            new() { Name = "Ege Toptan Gıda Ltd. Şti.", Kind = FirmKind.Corporate, City = "İzmir", District = "Bornova",
                    Address = "Sanayi Sitesi 3. Blok", TaxOffice = "Bornova", TaxNumber = "5554443332",
                    Email = "satis@egetoptan.com", Phone = "0232 555 44 33", IsCustomer = false, IsSupplier = true }
        };
        db.Firms.AddRange(firms);

        var products = new List<Product>
        {
            new() { Code = "URN-001", Name = "Zeytinyağı 5L", Unit = "Adet", SalePrice = 850, PurchasePrice = 600, VatRate = 1, StockAmount = 120 },
            new() { Code = "URN-002", Name = "Un 25Kg", Unit = "Adet", SalePrice = 450, PurchasePrice = 320, VatRate = 1, StockAmount = 80 },
            new() { Code = "URN-003", Name = "Ofis Sandalyesi", Unit = "Adet", SalePrice = 3200, PurchasePrice = 2100, VatRate = 20, StockAmount = 15 },
            new() { Code = "URN-004", Name = "A4 Fotokopi Kağıdı", Unit = "Koli", SalePrice = 750, PurchasePrice = 520, VatRate = 20, StockAmount = 200 }
        };
        db.Products.AddRange(products);

        var services = new List<Service>
        {
            new() { Code = "HZM-001", Name = "Danışmanlık Hizmeti", Unit = "Saat", Price = 1500, VatRate = 20 },
            new() { Code = "HZM-002", Name = "Nakliye", Unit = "Sefer", Price = 2500, VatRate = 20 }
        };
        db.Services.AddRange(services);

        db.Safes.Add(new Safe { Name = "Merkez Kasa", OpeningBalance = 2500 });
        db.BankAccounts.Add(new BankAccount
        {
            Name = "Vadesiz TL Hesabı", BankName = "İş Bankası", Branch = "Kadıköy",
            Iban = "TR12 0006 4000 0011 2345 6789 01", OpeningBalance = 15000
        });
        db.SaveChanges();

        var year = DateTime.Today.Year;
        var invoices = new List<Invoice>();
        int no = 1;

        Invoice MakeInvoice(InvoiceType type, Firm firm, DateTime date, params InvoiceLine[] lines)
        {
            var inv = new Invoice
            {
                InvoiceNumber = $"ISB{year}{no++:D9}",
                Type = type,
                FirmId = firm.Id,
                InvoiceDate = date,
                DueDate = date.AddDays(30),
                Status = date < DateTime.Today.AddDays(-45) ? InvoiceStatus.Paid : InvoiceStatus.Open,
                Lines = lines.ToList()
            };
            InvoiceCalculator.Calculate(inv);
            return inv;
        }

        InvoiceLine Line(Product p, decimal qty, bool sales = true) => new()
        {
            ProductId = p.Id, ItemName = p.Name, Quantity = qty, Unit = p.Unit,
            UnitPrice = sales ? p.SalePrice : p.PurchasePrice, VatRate = p.VatRate
        };

        invoices.Add(MakeInvoice(InvoiceType.SalesWholesale, firms[0], new DateTime(year, 1, 15),
            Line(products[0], 20), Line(products[1], 10)));
        invoices.Add(MakeInvoice(InvoiceType.SalesWholesale, firms[1], new DateTime(year, 2, 10),
            Line(products[2], 4), Line(products[3], 12)));
        invoices.Add(MakeInvoice(InvoiceType.SalesRetail, firms[2], new DateTime(year, 3, 5),
            Line(products[0], 2)));
        invoices.Add(MakeInvoice(InvoiceType.SalesWholesale, firms[0], new DateTime(year, 4, 22),
            Line(products[3], 30)));
        invoices.Add(MakeInvoice(InvoiceType.SalesWholesale, firms[1], new DateTime(year, 5, 18),
            Line(products[0], 15), Line(products[2], 2)));
        invoices.Add(MakeInvoice(InvoiceType.SalesRetail, firms[2], new DateTime(year, 6, 8),
            Line(products[1], 3)));

        invoices.Add(MakeInvoice(InvoiceType.Purchase, firms[3], new DateTime(year, 1, 8),
            Line(products[0], 50, sales: false), Line(products[1], 40, sales: false)));
        invoices.Add(MakeInvoice(InvoiceType.Purchase, firms[3], new DateTime(year, 3, 12),
            Line(products[3], 100, sales: false)));
        invoices.Add(MakeInvoice(InvoiceType.Purchase, firms[3], new DateTime(year, 5, 25),
            Line(products[2], 10, sales: false)));

        InvoiceLine ServiceLine(Service s, decimal qty) => new()
        {
            ServiceId = s.Id, ItemName = s.Name, Quantity = qty, Unit = s.Unit,
            UnitPrice = s.Price, VatRate = s.VatRate
        };

        invoices.Add(MakeInvoice(InvoiceType.Expense, firms[3], new DateTime(year, 2, 20),
            ServiceLine(services[1], 2)));
        invoices.Add(MakeInvoice(InvoiceType.Expense, firms[3], new DateTime(year, 4, 15),
            ServiceLine(services[0], 5)));

        db.Invoices.AddRange(invoices);
        db.SaveChanges();

        // Ödendi durumundaki faturalara tam tahsilat/ödeme kaydı düşülür (bankaya)
        var bank = db.BankAccounts.First();
        foreach (var invoice in invoices.Where(i => i.Status == InvoiceStatus.Paid))
        {
            db.Payments.Add(new Payment
            {
                InvoiceId = invoice.Id,
                Date = invoice.InvoiceDate.AddDays(15),
                Amount = invoice.GrandTotal,
                AccountType = PaymentAccountType.Bank,
                BankAccountId = bank.Id,
                Description = "Açılış verisi"
            });
        }
        db.SaveChanges();
    }
}
