namespace SecureLedger.Api.Models;

// "users" tablosunun C# karşılığı: sistemi kullanan kişiler (personel/yönetici).
public class User
{
    // IDENTITY ALWAYS: değeri her zaman veritabanı üretir, biz atamayız.
    public long Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    // Parolanın kendisi değil, güçlü bir algoritmayla üretilmiş hash'i saklanır.
    public string PasswordHash { get; set; } = null!;

    // Varsayılan değeri veritabanı verir: now()
    public DateTime CreatedAt { get; set; }

    // Bu kullanıcının yaptığı işlemlere ait audit kayıtları.
    public List<AuditLog> AuditLogs { get; set; } = [];
}
