namespace Isbasi.Web.Models;

/// <summary>
/// Yumuşak silinen belgeler: "Sil" kaydı veritabanından kaldırmaz, işaretler.
/// Global query filter (AppDbContext) işaretli kayıtları tüm sorgulardan gizler;
/// çöp kutusu sayfası IgnoreQueryFilters ile listeler, geri alır ya da kalıcı siler.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
