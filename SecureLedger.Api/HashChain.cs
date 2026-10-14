using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Defterin her sayfası için mühür (hash) hesaplar.
//
// Mühür = HMAC-SHA256( gizli anahtar, sayfanın içeriği + önceki sayfanın mührü )
//
// - İşlemde tek bir şey değişirse (ör. tutar) mühür tutmaz.
// - Her mühür bir öncekine bağlı olduğu için arada sayfa silinirse zincir kopar.
// - Gizli anahtar olmadan kimse yeni ve geçerli bir mühür üretemez.
public class HashChain
{
    private readonly byte[] _key;

    // Aynı anda iki işlem gelirse ikisi de aynı "önceki mühür"ü alıp zinciri çatallamasın diye
    // sayfalar sırayla, tek tek yazılır.
    public SemaphoreSlim WriteLock { get; } = new(1, 1);

    public HashChain(IConfiguration config)
    {
        // Anahtar user-secrets'ta durur (koda yazılmaz).
        var key = config["HashChain:Key"]
            ?? throw new Exception("HashChain:Key bulunamadı (user-secrets).");

        _key = Convert.FromBase64String(key);
    }

    public string ComputeHash(Transaction t, long userId, string action, string? previousHash)
    {
        // Mühürlenecek bilgileri tek bir yazıya çevir (JSON).
        // Tutar her zaman "250.00" biçiminde yazılır ki hesap her seferinde aynı çıksın.
        var content = JsonSerializer.Serialize(new
        {
            t.Id,
            t.CustomerId,
            Amount = t.Amount.ToString("F2", CultureInfo.InvariantCulture),
            t.Currency,
            t.Type,
            t.Description,
            CreatedAt = t.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            userId,
            action,
            previousHash
        });

        byte[] hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(content));

        // 64 karakterlik yazıya çevir (ör. "a7f3...")
        return Convert.ToHexStringLower(hash);
    }
}
