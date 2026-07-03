using Isbasi.Web.Data;
using Isbasi.Web.Models;

namespace Isbasi.Tests;

/// <summary>StockCalculator, CashBalanceCalculator ve InvoiceNumbers'ın veritabanı davranışları.</summary>
public class CalculatorDbTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Stok_SatisDuser_AlisArtar_SiparisEtkilemez()
    {
        var firm = TestData.AddFirm(_db.Context);
        var product = TestData.AddProduct(_db.Context, stock: 100);

        TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100, quantity: 10, product: product);
        TestData.AddInvoice(_db.Context, InvoiceType.Purchase, firm, 60, quantity: 5, product: product);
        // Siparişler stok hesabına girmemeli — girerse bu test yakalar
        TestData.AddInvoice(_db.Context, InvoiceType.SalesOrder, firm, 100, quantity: 50, product: product);
        TestData.AddInvoice(_db.Context, InvoiceType.PurchaseOrder, firm, 60, quantity: 70, product: product);

        var stocks = await StockCalculator.Compute(_db.Context);

        Assert.Equal(95m, stocks[product.Id]);   // 100 - 10 + 5
    }

    [Fact]
    public async Task KasaBakiyesi_TahsilatArti_OdemeEksi()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context, opening: 1000);
        var sales = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 500);
        var purchase = TestData.AddInvoice(_db.Context, InvoiceType.Purchase, firm, 300);

        _db.Context.Payments.Add(new Payment { InvoiceId = sales.Id, Amount = 600, AccountType = PaymentAccountType.Safe, SafeId = safe.Id });
        _db.Context.Payments.Add(new Payment { InvoiceId = purchase.Id, Amount = 360, AccountType = PaymentAccountType.Safe, SafeId = safe.Id });
        _db.Context.SaveChanges();

        var balances = await CashBalanceCalculator.Compute(_db.Context);

        Assert.Equal(1240m, balances.SafeBalances[safe.Id]);   // 1000 + 600 - 360
    }

    [Fact]
    public async Task KasaBakiyesi_TahsilEdilenCekGirer_PortfoydekiGirmez()
    {
        var firm = TestData.AddFirm(_db.Context);
        var bank = TestData.AddBank(_db.Context, opening: 0);

        _db.Context.Cheques.Add(new Cheque
        {
            Type = ChequeType.Received, FirmId = firm.Id, ChequeNumber = "C1",
            Amount = 8500, Status = ChequeStatus.Cleared, BankAccountId = bank.Id
        });
        _db.Context.Cheques.Add(new Cheque
        {
            Type = ChequeType.Issued, FirmId = firm.Id, ChequeNumber = "C2",
            Amount = 2000, Status = ChequeStatus.Cleared, BankAccountId = bank.Id
        });
        _db.Context.Cheques.Add(new Cheque
        {
            Type = ChequeType.Received, FirmId = firm.Id, ChequeNumber = "C3",
            Amount = 99999, Status = ChequeStatus.Portfolio        // bakiyeye girmemeli
        });
        _db.Context.SaveChanges();

        var balances = await CashBalanceCalculator.Compute(_db.Context);

        Assert.Equal(6500m, balances.BankBalances[bank.Id]);   // +8500 - 2000
    }

    [Fact]
    public async Task BelgeNumaralari_SeriBazindaArtar()
    {
        var firm = TestData.AddFirm(_db.Context);
        int year = DateTime.Today.Year;

        var first = await InvoiceNumbers.Next(_db.Context, "ISB");
        Assert.Equal($"ISB{year}000000001", first);

        var invoice = TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 100);
        invoice.InvoiceNumber = first;
        _db.Context.SaveChanges();

        Assert.Equal($"ISB{year}000000002", await InvoiceNumbers.Next(_db.Context, "ISB"));
        // Farklı seri kendi sayacını kullanır
        Assert.Equal($"SIP{year}000000001", await InvoiceNumbers.Next(_db.Context, "SIP"));
    }
}
