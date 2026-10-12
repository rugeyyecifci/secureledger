using Microsoft.EntityFrameworkCore;
using SecureLedger.Api.Models;

namespace SecureLedger.Api.Data;

// DbContext: Uygulama ile veritabanı arasındaki köprü.
// Tablolar DBeaver'da elle oluşturuldu; burada sadece MEVCUT tablolara eşleme yapıyoruz.
// Migration kullanılmıyor, bu yüzden buradaki ayarlar veritabanı şemasını DEĞİŞTİRMEZ.
public class SecureLedgerDbContext(DbContextOptions<SecureLedgerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- users ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);

            // GENERATED ALWAYS AS IDENTITY: EF Core INSERT sırasında id göndermez.
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(100);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");

            // Değer verilmezse veritabanındaki now() kullanılır.
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ---------- customers ----------
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(100);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.EncryptedPhone).HasColumnName("encrypted_phone");
            e.Property(x => x.EncryptedAddress).HasColumnName("encrypted_address");
            e.Property(x => x.EncryptionKeyVersion).HasColumnName("encryption_key_version");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        // ---------- transactions ----------
        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.CustomerId).HasColumnName("customer_id");

            // numeric(15,2): toplam 15 basamak, virgülden sonra 2 basamak.
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(15, 2);
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
            e.Property(x => x.TransactionType).HasColumnName("transaction_type").HasMaxLength(20);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            // Bir müşterinin birden çok işlemi olur.
            // Veritabanında ON DELETE kuralı yok (NO ACTION): işlemi olan müşteri silinemez.
            e.HasOne(x => x.Customer)
                .WithMany(c => c.Transactions)
                .HasForeignKey(x => x.CustomerId)
                .HasConstraintName("fk_transaction_customer")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- audit_logs ----------
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.TransactionId).HasColumnName("transaction_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(30);
            e.Property(x => x.PreviousHash).HasColumnName("previous_hash").HasMaxLength(64);
            e.Property(x => x.CurrentHash).HasColumnName("current_hash").HasMaxLength(64);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Transaction)
                .WithMany(t => t.AuditLogs)
                .HasForeignKey(x => x.TransactionId)
                .HasConstraintName("fk_audit_transaction")
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_audit_user")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
