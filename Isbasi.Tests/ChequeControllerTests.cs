using Isbasi.Web.Controllers;
using Isbasi.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Isbasi.Tests;

public class ChequeControllerTests : IDisposable
{
    private readonly TestDb _db = new();
    public void Dispose() => _db.Dispose();

    private ChequeController Controller() => new ChequeController(_db.Context).WithTempData();

    private Cheque NewCheque(int firmId, ChequeStatus status, PaymentAccountType accountType,
        int? safeId = null, int? bankId = null) => new()
    {
        Type = ChequeType.Received,
        FirmId = firmId,
        ChequeNumber = "TEST-1",
        Amount = 1000,
        Status = status,
        AccountType = accountType,
        SafeId = safeId,
        BankAccountId = bankId
    };

    [Fact]
    public async Task TahsilEdildi_HesapSecilmedigindeReddedilir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var controller = Controller();

        var result = await controller.Save(NewCheque(firm.Id, ChequeStatus.Cleared, PaymentAccountType.Safe));

        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ViewResult>(result);
        Assert.Empty(_db.Context.Cheques);
    }

    [Fact]
    public async Task TahsilEdildi_KasaSecilince_BankaAlaniTemizlenir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);
        var bank = TestData.AddBank(_db.Context);

        // İkisi de dolu gelirse hesap tipi kazanmalı; sızıntı bakiyeyi bozar
        await Controller().Save(NewCheque(firm.Id, ChequeStatus.Cleared, PaymentAccountType.Safe,
            safeId: safe.Id, bankId: bank.Id));

        var saved = _db.Context.Cheques.Single();
        Assert.Equal(safe.Id, saved.SafeId);
        Assert.Null(saved.BankAccountId);
        Assert.NotNull(saved.ClearedDate);   // boş bırakılınca bugünle doldurulur
    }

    [Fact]
    public async Task PortfoyeAlinan_TahsilBilgileriTemizlenir()
    {
        var firm = TestData.AddFirm(_db.Context);
        var safe = TestData.AddSafe(_db.Context);

        // Tahsil edilmişken portföye geri çekiliyor: hesap/tarih kalıntısı bakiyeyi şişirirdi
        var cheque = NewCheque(firm.Id, ChequeStatus.Portfolio, PaymentAccountType.Safe, safeId: safe.Id);
        cheque.ClearedDate = DateTime.Today;
        await Controller().Save(cheque);

        var saved = _db.Context.Cheques.Single();
        Assert.Null(saved.SafeId);
        Assert.Null(saved.ClearedDate);
    }

    [Fact]
    public async Task FirmaSecilmeden_CekKaydedilemez()
    {
        var controller = Controller();

        var result = await controller.Save(NewCheque(0, ChequeStatus.Portfolio, PaymentAccountType.Safe));

        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(_db.Context.Cheques);
    }
}
