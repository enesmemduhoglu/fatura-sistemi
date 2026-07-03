using Isbasi.Web.Models;

namespace Isbasi.Web.ViewModels;

public class StatementRow
{
    public DateTime Date { get; set; }
    public string DocNo { get; set; } = "";
    public string DocType { get; set; } = "";
    public string? Description { get; set; }
    public decimal Debit { get; set; }    // borç: firmanın bize borcunu artırır
    public decimal Credit { get; set; }   // alacak: borcunu azaltır / bizim borcumuzu artırır
    public decimal Balance { get; set; }  // yürüyen bakiye (borç − alacak)
    public string? Link { get; set; }
}

public class FirmStatementViewModel
{
    public Firm Firm { get; set; } = new();
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public decimal OpeningBalance { get; set; }   // tarih filtresi öncesi devir
    public bool HasOpening { get; set; }
    public List<StatementRow> Rows { get; set; } = new();

    public decimal TotalDebit => Rows.Sum(r => r.Debit);
    public decimal TotalCredit => Rows.Sum(r => r.Credit);
    public decimal Balance => Rows.Count > 0 ? Rows[^1].Balance : OpeningBalance;
}
