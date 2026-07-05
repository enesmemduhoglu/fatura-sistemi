using System.Collections.Concurrent;

namespace Isbasi.Web.Data;

/// <summary>
/// Kaba kuvvet (brute force) koruması: aynı e-posta için 15 dakika içinde
/// 5 hatalı giriş denemesinden sonra o e-posta ile giriş geçici olarak engellenir.
/// Başarılı giriş sayacı sıfırlar. Kayıtlar bellekte tutulur (tek instance yeterli).
/// </summary>
public class LoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Func<DateTime> _clock;
    private readonly ConcurrentDictionary<string, (int Count, DateTime FirstFailure)> _failures = new();

    public LoginThrottle() : this(() => DateTime.UtcNow) { }

    /// <summary>Testlerin zamanı ilerletebilmesi için saat dışarıdan verilebilir.</summary>
    public LoginThrottle(Func<DateTime> clock) => _clock = clock;

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public bool IsBlocked(string email)
    {
        if (!_failures.TryGetValue(Normalize(email), out var entry)) return false;
        if (_clock() - entry.FirstFailure >= Window)
        {
            _failures.TryRemove(Normalize(email), out _);
            return false;
        }
        return entry.Count >= MaxFailures;
    }

    public void RecordFailure(string email)
    {
        string key = Normalize(email);
        var now = _clock();
        _failures.AddOrUpdate(key,
            _ => (1, now),
            (_, entry) => now - entry.FirstFailure >= Window
                ? (1, now)
                : (entry.Count + 1, entry.FirstFailure));
    }

    public void RecordSuccess(string email) => _failures.TryRemove(Normalize(email), out _);
}
