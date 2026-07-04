using Isbasi.Web.Data;

namespace Isbasi.Tests;

public class AttachmentStorageTests
{
    [Theory]
    [InlineData("belge.pdf", true)]
    [InlineData("BELGE.PDF", true)]          // uzantı büyük/küçük harf duyarsız
    [InlineData("resim.jpeg", true)]
    [InlineData("tablo.xlsx", true)]
    [InlineData("arsiv.zip", true)]
    [InlineData("zararli.exe", false)]
    [InlineData("betik.bat", false)]
    [InlineData("uzantisiz", false)]
    [InlineData("nokta.ile.biten.", false)]
    [InlineData("gizli.pdf.exe", false)]     // yalnız son uzantıya bakılır
    public void IsAllowed_UzantiListesineGoreKararVerir(string fileName, bool expected)
        => Assert.Equal(expected, AttachmentStorage.IsAllowed(fileName));
}
