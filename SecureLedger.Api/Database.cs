using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

// Uygulama ile veritabanı arasındaki bağlantı.
// db.Customers yazınca "customers" tablosuna ulaşırız.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
