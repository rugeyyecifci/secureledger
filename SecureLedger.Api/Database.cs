using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

// Veritabanındaki "users" tablosunun C# hâli: sisteme giriş yapan kişiler.
[Table("users")]
public class User
{
    [Column("id")]
    public long Id { get; set; }                     // Veritabanı otomatik verir

    [Column("full_name")]
    public string FullName { get; set; } = "";

    [Column("email")]
    public string Email { get; set; } = "";

    [Column("password_hash")]
    public string PasswordHash { get; set; } = "";   // Şifrenin kendisi DEĞİL, hash'i

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }          // Veritabanı otomatik verir
}

// Veritabanındaki "customers" tablosunun C# hâli.
// [Column("...")] : C#'taki isim ile tablodaki sütun adını eşleştirir.
[Table("customers")]
public class Customer
{
    [Column("id")]
    public long Id { get; set; }                     // Veritabanı otomatik verir

    [Column("full_name")]
    public string FullName { get; set; } = "";

    [Column("email")]
    public string? Email { get; set; }               // ? = boş olabilir

    [Column("encrypted_phone")]
    public byte[]? EncryptedPhone { get; set; }      // Şifreli telefon

    [Column("encrypted_address")]
    public byte[]? EncryptedAddress { get; set; }    // Şifreli adres

    [Column("encryption_key_version")]
    public int EncryptionKeyVersion { get; set; } = 1;

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }          // Veritabanı otomatik verir
}

// Veritabanındaki "transactions" tablosunun C# hâli: bir müşterinin ödeme veya iadesi.
[Table("transactions")]
public class Transaction
{
    [Column("id")]
    public long Id { get; set; }                     // Veritabanı otomatik verir

    [Column("customer_id")]
    public long CustomerId { get; set; }             // Hangi müşteriye ait

    [Column("amount")]
    public decimal Amount { get; set; }              // Para için her zaman decimal

    [Column("currency")]
    public string Currency { get; set; } = "TRY";

    [Column("transaction_type")]
    public string Type { get; set; } = "";           // "PAYMENT" veya "REFUND"

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }          // Veritabanı otomatik verir
}

// Veritabanındaki "audit_logs" tablosunun C# hâli: defterin bir sayfası.
// Her sayfa bir işlemi kaydeder ve bir önceki sayfanın mührüne bağlıdır (hash zinciri).
[Table("audit_logs")]
public class AuditLog
{
    [Column("id")]
    public long Id { get; set; }                     // Veritabanı otomatik verir

    [Column("transaction_id")]
    public long TransactionId { get; set; }          // Hangi işlem

    [Column("user_id")]
    public long UserId { get; set; }                 // Kim yaptı

    [Column("action")]
    public string Action { get; set; } = "";         // Ne yaptı: "CREATE"

    [Column("previous_hash")]
    public string? PreviousHash { get; set; }        // Önceki sayfanın mührü (ilk sayfada boş)

    [Column("current_hash")]
    public string CurrentHash { get; set; } = "";    // Bu sayfanın mührü

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }          // Veritabanı otomatik verir
}

// Uygulama ile veritabanı arasındaki bağlantı.
// db.Users -> "users", db.Customers -> "customers", db.Transactions -> "transactions", db.AuditLogs -> "audit_logs"
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
