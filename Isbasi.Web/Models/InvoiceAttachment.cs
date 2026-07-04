namespace Isbasi.Web.Models;

/// <summary>
/// Faturaya eklenen belge. Dosya içeriği diskte (AttachmentStorage) GUID adla durur;
/// veritabanında yalnız meta bilgisi tutulur. Fatura silinince kayıt cascade silinir,
/// fiziksel dosyayı controller temizler.
/// </summary>
public class InvoiceAttachment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>Kullanıcının yüklediği orijinal ad; indirmede bu adla sunulur.</summary>
    public string FileName { get; set; } = "";

    /// <summary>Diskteki ad (GUID + uzantı); yol gezinmesine kapalı.</summary>
    public string StoredName { get; set; } = "";

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
