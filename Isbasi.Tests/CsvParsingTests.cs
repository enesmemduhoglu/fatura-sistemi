using Isbasi.Web.Controllers;

namespace Isbasi.Tests;

/// <summary>
/// CSV içe aktarma sayı ayrıştırma regresyonu: "85.25" tr kültürüyle 8525
/// okunuyordu (binlik ayraç sanılıyordu, commit aedc270'te düzeltildi).
/// </summary>
public class CsvParsingTests
{
    [Theory]
    [InlineData("1.250,50", 1250.50)]   // Türkçe: binlik nokta + ondalık virgül
    [InlineData("85,25", 85.25)]        // Türkçe: yalnız ondalık virgül
    [InlineData("85.25", 85.25)]        // Nokta ondalık — regresyon: 8525 OLMAMALI
    [InlineData("1250.5", 1250.5)]
    [InlineData("1250", 1250)]
    [InlineData("0", 0)]
    [InlineData("3,5", 3.5)]
    public void ParseDecimal_IkiBicimiDeDogruOkur(string input, decimal expected)
    {
        Assert.Equal(expected, ManageController.ParseDecimal(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void ParseDecimal_GecersizDeger_VarsayilanaDuser(string input)
    {
        Assert.Equal(20m, ManageController.ParseDecimal(input, fallback: 20));
    }
}
