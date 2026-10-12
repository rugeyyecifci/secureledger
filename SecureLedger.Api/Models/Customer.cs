namespace SecureLedger.Api.Models;

// "customers" tablosunun C# karşılığı.
public class Customer
{
    // IDENTITY ALWAYS: değeri her zaman veritabanı üretir.
    public long Id { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    // AES-256-GCM ile şifrelenmiş ham baytlar (bytea). Şifreleme adımı henüz yazılmadı.
    public byte[]? EncryptedPhone { get; set; }

    public byte[]? EncryptedAddress { get; set; }

    // Hangi anahtar sürümüyle şifrelendiğini tutar; anahtar değişiminde (key rotation) işe yarar.
    public int EncryptionKeyVersion { get; set; } = 1;

    // Varsayılan değeri veritabanı verir: now()
    public DateTime CreatedAt { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
