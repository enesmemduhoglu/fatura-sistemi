namespace Isbasi.Web.ViewModels;

/// <summary>Çöp kutusundaki tek kayıt: tür etiketi + kimlik + özet bilgiler.</summary>
public class TrashRow
{
    public string Kind { get; set; } = "";        // invoice | payment | cheque | receipt
    public string KindLabel { get; set; } = "";
    public int Id { get; set; }
    public string Title { get; set; } = "";       // belge/çek/makbuz numarası
    public string? Detail { get; set; }           // firma adı vb.
    public decimal AmountTry { get; set; }
    public DateTime? DeletedAt { get; set; }
}
