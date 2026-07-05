namespace Isbasi.Web.Data;

/// <summary>
/// Fatura eki dosyalarını web kökü dışında saklar (varsayılan: App_Data/attachments,
/// statik sunulmaz; indirme yetkili controller aksiyonundan geçer). Kök,
/// "Attachments:Root" ayarıyla değiştirilebilir — testler geçici klasör verir.
/// </summary>
public class AttachmentStorage
{
    public const long MaxSize = 10 * 1024 * 1024; // 10 MB

    public static readonly string[] AllowedExtensions =
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".txt", ".csv", ".xls", ".xlsx", ".doc", ".docx", ".zip"
    };

    private readonly string _root;

    public AttachmentStorage(IConfiguration config, IWebHostEnvironment env)
        => _root = config["Attachments:Root"]
            ?? Path.Combine(env.ContentRootPath, "App_Data", "attachments");

    // İçerik türü istemcinin beyanına değil dosya uzantısına göre belirlenir;
    // aksi halde ör. text/html beyanıyla tarayıcıda çalışan içerik sunulabilir
    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".zip"] = "application/zip"
    };

    public static bool IsAllowed(string fileName)
        => AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public static string ContentTypeFor(string fileName)
        => ContentTypes.TryGetValue(Path.GetExtension(fileName).ToLowerInvariant(), out var type)
            ? type : "application/octet-stream";

    public string PathFor(string storedName) => Path.Combine(_root, storedName);

    /// <summary>Dosyayı GUID + orijinal uzantı adıyla diske yazar, disk adını döner.</summary>
    public async Task<string> SaveAsync(IFormFile file)
    {
        Directory.CreateDirectory(_root);
        string storedName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = File.Create(PathFor(storedName));
        await file.CopyToAsync(stream);
        return storedName;
    }

    public void Delete(string storedName)
    {
        string path = PathFor(storedName);
        if (File.Exists(path)) File.Delete(path);
    }
}
