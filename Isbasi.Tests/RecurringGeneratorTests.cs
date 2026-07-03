using Isbasi.Web.Data;
using Isbasi.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Isbasi.Tests;

public class RecurringGeneratorTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private Invoice AddSource(string currency = "TL", decimal rate = 1, DateTime? date = null, DateTime? due = null)
    {
        var firm = TestData.AddFirm(_db.Context);
        return TestData.AddInvoice(_db.Context, InvoiceType.SalesWholesale, firm, 1000,
            currency: currency, rate: rate,
            date: date ?? DateTime.Today.AddMonths(-6),
            due: due);
    }

    [Fact]
    public async Task GecmisDonemler_TekTekUretilir()
    {
        var source = AddSource();
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Monthly,
            NextRunDate = DateTime.Today.AddMonths(-2)   // 2 ay önce + bu ay = 3 dönem
        });
        _db.Context.SaveChanges();

        int generated = await RecurringGenerator.GenerateDue(_db.Context);

        Assert.Equal(3, generated);
        var plan = _db.Context.RecurringPlans.Single();
        Assert.Equal(3, plan.GeneratedCount);
        Assert.True(plan.NextRunDate > DateTime.Today);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public async Task VadeOfseti_KaynaktanKorunur()
    {
        // Kaynak: fatura tarihi ile vade arası 30 gün
        var source = AddSource(
            date: new DateTime(2026, 1, 15),
            due: new DateTime(2026, 2, 14));
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Monthly,
            NextRunDate = DateTime.Today
        });
        _db.Context.SaveChanges();

        await RecurringGenerator.GenerateDue(_db.Context);

        var generated = _db.Context.Invoices.Single(i => i.Id != source.Id);
        Assert.Equal(DateTime.Today, generated.InvoiceDate.Date);
        Assert.Equal(DateTime.Today.AddDays(30), generated.DueDate!.Value.Date);
        Assert.Equal(InvoiceStatus.Open, generated.Status);
    }

    [Fact]
    public async Task DovizVeKur_UretilenFaturayaTasinir()
    {
        var source = AddSource(currency: "USD", rate: 40.5m);
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Monthly,
            NextRunDate = DateTime.Today
        });
        _db.Context.SaveChanges();

        await RecurringGenerator.GenerateDue(_db.Context);

        var generated = _db.Context.Invoices.Include(i => i.Lines).Single(i => i.Id != source.Id);
        Assert.Equal("USD", generated.Currency);
        Assert.Equal(40.5m, generated.ExchangeRate);
        Assert.Equal(source.GrandTotal, generated.GrandTotal);
        Assert.Single(generated.Lines);
    }

    [Fact]
    public async Task BitisTarihiAsilinca_PlanPasiflesir_UretimYapilmaz()
    {
        var source = AddSource();
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Monthly,
            NextRunDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(-1)   // bitiş geçmişte
        });
        _db.Context.SaveChanges();

        int generated = await RecurringGenerator.GenerateDue(_db.Context);

        Assert.Equal(0, generated);
        Assert.False(_db.Context.RecurringPlans.Single().IsActive);
    }

    [Fact]
    public async Task PasifPlan_UretimYapmaz()
    {
        var source = AddSource();
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Monthly,
            NextRunDate = DateTime.Today.AddMonths(-3),
            IsActive = false
        });
        _db.Context.SaveChanges();

        Assert.Equal(0, await RecurringGenerator.GenerateDue(_db.Context));
    }

    [Fact]
    public async Task CatchUp_GuvenlikSiniri_SonsuzDongudenKorur()
    {
        var source = AddSource();
        _db.Context.RecurringPlans.Add(new RecurringPlan
        {
            SourceInvoiceId = source.Id,
            Frequency = RecurringFrequency.Weekly,
            NextRunDate = DateTime.Today.AddYears(-5)   // yüzlerce dönem geride
        });
        _db.Context.SaveChanges();

        int generated = await RecurringGenerator.GenerateDue(_db.Context);

        Assert.True(generated <= 24, $"Plan başına en çok 24 dönem üretilmeli, {generated} üretildi.");
    }
}
