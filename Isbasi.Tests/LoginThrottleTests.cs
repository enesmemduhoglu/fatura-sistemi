using Isbasi.Web.Data;

namespace Isbasi.Tests;

public class LoginThrottleTests
{
    [Fact]
    public void EsikAltindakiDenemeler_Engellenmez()
    {
        var throttle = new LoginThrottle();
        for (int i = 0; i < LoginThrottle.MaxFailures - 1; i++)
            throttle.RecordFailure("a@b.com");

        Assert.False(throttle.IsBlocked("a@b.com"));
    }

    [Fact]
    public void EsikKadarHataliDeneme_Engeller()
    {
        var throttle = new LoginThrottle();
        for (int i = 0; i < LoginThrottle.MaxFailures; i++)
            throttle.RecordFailure("a@b.com");

        Assert.True(throttle.IsBlocked("a@b.com"));
        Assert.False(throttle.IsBlocked("baska@b.com"));   // yalnızca o e-posta kilitlenir
    }

    [Fact]
    public void EpostaKarsilastirmasi_BuyukKucukHarfVeBosluktanEtkilenmez()
    {
        var throttle = new LoginThrottle();
        for (int i = 0; i < LoginThrottle.MaxFailures; i++)
            throttle.RecordFailure("  A@B.COM ");

        Assert.True(throttle.IsBlocked("a@b.com"));
    }

    [Fact]
    public void BasariliGiris_SayaciSifirlar()
    {
        var throttle = new LoginThrottle();
        for (int i = 0; i < LoginThrottle.MaxFailures - 1; i++)
            throttle.RecordFailure("a@b.com");
        throttle.RecordSuccess("a@b.com");
        throttle.RecordFailure("a@b.com");

        Assert.False(throttle.IsBlocked("a@b.com"));
    }

    [Fact]
    public void PencereSuresiDolunca_EngelKalkar()
    {
        var now = DateTime.UtcNow;
        var throttle = new LoginThrottle(() => now);
        for (int i = 0; i < LoginThrottle.MaxFailures; i++)
            throttle.RecordFailure("a@b.com");
        Assert.True(throttle.IsBlocked("a@b.com"));

        now += LoginThrottle.Window + TimeSpan.FromSeconds(1);
        Assert.False(throttle.IsBlocked("a@b.com"));
    }

    [Fact]
    public void PencereDolduktanSonrakiHata_YeniPencereBaslatir()
    {
        var now = DateTime.UtcNow;
        var throttle = new LoginThrottle(() => now);
        for (int i = 0; i < LoginThrottle.MaxFailures; i++)
            throttle.RecordFailure("a@b.com");

        now += LoginThrottle.Window + TimeSpan.FromSeconds(1);
        throttle.RecordFailure("a@b.com");   // eski pencere düştü, sayaç 1'den başlar

        Assert.False(throttle.IsBlocked("a@b.com"));
    }
}
