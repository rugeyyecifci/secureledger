namespace SecureLedger.Api.Models;

// "audit_logs" tablosunun C# karşılığı: kim, hangi işlem üzerinde, ne yaptı.
// Hash zinciri de bu tabloda tutulur (previous_hash -> current_hash).
public class AuditLog
{
    // IDENTITY ALWAYS: değeri her zaman veritabanı üretir.
    public long Id { get; set; }

    // Şu an zorunlu; audit adımında nullable yapılıp entity_type/entity_id eklenecek.
    public long TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public long UserId { get; set; }
    public User User { get; set; } = null!;

    // Veritabanında CHECK: sadece "CREATE", "UPDATE" veya "DELETE"
    public string Action { get; set; } = null!;

    // Zincirdeki bir önceki kaydın hash'i (64 hex karakter). İlk kayıtta boştur.
    public string? PreviousHash { get; set; }

    // Bu kaydın hash'i (64 hex karakter).
    public string CurrentHash { get; set; } = null!;

    // Varsayılan değeri veritabanı verir: now()
    public DateTime CreatedAt { get; set; }
}
