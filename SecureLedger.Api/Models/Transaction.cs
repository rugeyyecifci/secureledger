namespace SecureLedger.Api.Models;

// "transactions" tablosunun C# karşılığı: bir müşteriye ait ödeme/iade kaydı.
public class Transaction
{
    // IDENTITY ALWAYS: değeri her zaman veritabanı üretir.
    public long Id { get; set; }

    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // numeric(15,2). Para için float/double değil decimal kullanılır (yuvarlama hatası olmasın diye).
    // Veritabanında CHECK: amount > 0
    public decimal Amount { get; set; }

    // ISO 4217 para birimi kodu, ör. "TRY"
    public string Currency { get; set; } = "TRY";

    // Veritabanında CHECK: sadece "PAYMENT" veya "REFUND"
    public string TransactionType { get; set; } = null!;

    public string? Description { get; set; }

    // Varsayılan değeri veritabanı verir: now()
    public DateTime CreatedAt { get; set; }

    public List<AuditLog> AuditLogs { get; set; } = [];
}
