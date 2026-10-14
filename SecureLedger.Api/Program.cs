using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1) AYARLAR ----------

// Veritabanı bağlantısı. Bilgi user-secrets'tan okunur (koda yazılmaz).
var connectionString = builder.Configuration.GetConnectionString("SecureLedgerDb");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Şifreleme servisi. Uygulama boyunca tek bir tane kullanılır.
builder.Services.AddSingleton<Encryption>();

// Hash zinciri (mühür) servisi. Uygulama boyunca tek bir tane kullanılır.
builder.Services.AddSingleton<HashChain>();

// Şifreleri güvenli şekilde hash'ler (PBKDF2, 100.000 tur). ASP.NET Core'un hazır aracı.
builder.Services.AddSingleton<PasswordHasher<User>>();

// Giriş anahtarı (token): Giriş yapınca verilir, sonraki isteklerde gönderilir.
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();

// Varsayılan kural: Her endpoint için GİRİŞ ZORUNLU.
// Sadece .AllowAnonymous() yazdıklarımız (kayıt ol, giriş yap) girişsiz çalışır.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Deneme sınırı: Aynı IP adresinden 1 dakikada en fazla 5 giriş/kayıt denemesi.
// Böylece biri şifreyi deneye deneye tahmin edemez. 6. deneme 429 hatası alır.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",   // Her IP'nin sayacı ayrı
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,                    // En fazla 5 deneme
            Window = TimeSpan.FromMinutes(1)    // 1 dakika içinde
        }));
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();   // API dokümanı: /openapi/v1.json
}

app.UseHttpsRedirection();
app.UseAuthentication();   // İstekteki giriş anahtarını oku: "Bu kim?"
app.UseAuthorization();    // Kurala bak: "Girmesine izin var mı?"
app.UseRateLimiter();      // Deneme sayısını say: "Çok mu deniyor?"


// ---------- 2) ENDPOINT'LER ----------

// Kayıt ol: şifre hash'lenerek kaydedilir (şifrenin kendisi asla saklanmaz).
app.MapPost("/api/auth/register", async (RegisterRequest input, AppDbContext db, PasswordHasher<User> hasher) =>
{
    if (string.IsNullOrWhiteSpace(input.FullName))
        return Results.BadRequest("Ad soyad zorunlu.");

    if (string.IsNullOrWhiteSpace(input.Email))
        return Results.BadRequest("E-posta zorunlu.");

    if (input.Password == null || input.Password.Length < 8)
        return Results.BadRequest("Şifre en az 8 karakter olmalı.");

    var email = input.Email.Trim().ToLowerInvariant();   // "Ali@X.com" ile "ali@x.com" aynı sayılsın
    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.Conflict("Bu e-posta zaten kayıtlı.");

    var user = new User { FullName = input.FullName.Trim(), Email = email };
    user.PasswordHash = hasher.HashPassword(user, input.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { user.Id, Message = "Kayıt başarılı." });
}).AllowAnonymous().RequireRateLimiting("auth");

// Giriş yap: e-posta ve şifre doğruysa bir giriş anahtarı (token) verilir.
app.MapPost("/api/auth/login", async (LoginRequest input, AppDbContext db, PasswordHasher<User> hasher) =>
{
    var email = (input.Email ?? "").Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

    // E-posta yanlış da olsa şifre yanlış da olsa AYNI mesaj:
    // böylece kimse "bu e-posta sistemde var mı?" diye deneyerek öğrenemez.
    if (user == null ||
        hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password ?? "") == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    // Anahtarın içine "bu kişi kim" bilgisini koy
    var identity = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName)
    ], BearerTokenDefaults.AuthenticationScheme);

    // Cevap: { accessToken: "...", expiresIn: 3600, ... }
    return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
}).AllowAnonymous().RequireRateLimiting("auth");

// Müşterileri listele: telefon ve adresi çözerek göster.
app.MapGet("/api/customers", async (AppDbContext db, Encryption enc) =>
{
    var customers = await db.Customers.OrderBy(c => c.Id).ToListAsync();

    return customers.Select(c => new
    {
        c.Id,
        c.FullName,
        c.Email,
        Phone = enc.Decrypt(c.EncryptedPhone),
        Address = enc.Decrypt(c.EncryptedAddress),
        c.CreatedAt
    });
});

// Müşteri ekle: telefon ve adresi şifreleyerek kaydet.
app.MapPost("/api/customers", async (NewCustomer input, AppDbContext db, Encryption enc) =>
{
    if (string.IsNullOrWhiteSpace(input.FullName))
        return Results.BadRequest("Ad soyad zorunlu.");

    if (input.Email != null && await db.Customers.AnyAsync(c => c.Email == input.Email))
        return Results.Conflict("Bu e-posta zaten kayıtlı.");

    var customer = new Customer
    {
        FullName = input.FullName,
        Email = input.Email,
        EncryptedPhone = enc.Encrypt(input.Phone),
        EncryptedAddress = enc.Encrypt(input.Address)
    };

    db.Customers.Add(customer);
    await db.SaveChangesAsync();   // Veritabanına yaz

    return Results.Ok(new { customer.Id, Message = "Müşteri eklendi." });
});

// İşlemleri listele.
app.MapGet("/api/transactions", async (AppDbContext db) =>
{
    return await db.Transactions.OrderBy(t => t.Id).ToListAsync();
});

// İşlem ekle (ödeme veya iade) ve deftere mühürlü bir sayfa yaz.
app.MapPost("/api/transactions", async (NewTransaction input, AppDbContext db, HashChain chain, ClaimsPrincipal currentUser) =>
{
    // Giriş anahtarından "işlemi kim yapıyor" bilgisini al
    var userId = long.Parse(currentUser.FindFirstValue(ClaimTypes.NameIdentifier)!);

    if (input.Amount <= 0 || input.Amount >= 1_000_000_000_000m)
        return Results.BadRequest("Tutar 0'dan büyük ve 1 trilyondan küçük olmalı.");

    if (input.Amount != Math.Round(input.Amount, 2))
        return Results.BadRequest("Tutarda virgülden sonra en fazla 2 basamak olabilir.");

    if (input.Type != "PAYMENT" && input.Type != "REFUND")
        return Results.BadRequest("Tür PAYMENT veya REFUND olmalı.");

    var currency = input.Currency ?? "TRY";   // Para birimi verilmezse TRY
    if (currency.Length != 3)
        return Results.BadRequest("Para birimi 3 harf olmalı (ör. TRY, USD).");

    if (!await db.Customers.AnyAsync(c => c.Id == input.CustomerId))
        return Results.NotFound("Müşteri bulunamadı.");

    var transaction = new Transaction
    {
        CustomerId = input.CustomerId,
        Amount = input.Amount,
        Currency = currency,
        Type = input.Type,
        Description = input.Description
    };

    // Sayfalar sırayla yazılsın (bkz. HashChain.WriteLock)
    await chain.WriteLock.WaitAsync();
    try
    {
        // İşlem ve defter sayfası birlikte kaydedilir: biri olmazsa ikisi de iptal.
        using var dbTransaction = await db.Database.BeginTransactionAsync();

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();   // İşlemi yaz (id ve tarih veritabanından gelir)

        // Defterdeki son sayfanın mührü = bu sayfanın "önceki mührü"
        var previousHash = await db.AuditLogs
            .OrderByDescending(a => a.Id)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync();

        db.AuditLogs.Add(new AuditLog
        {
            TransactionId = transaction.Id,
            UserId = userId,
            Action = "CREATE",
            PreviousHash = previousHash,
            CurrentHash = chain.ComputeHash(transaction, userId, "CREATE", previousHash)
        });
        await db.SaveChangesAsync();   // Sayfayı yaz

        await dbTransaction.CommitAsync();   // İkisini birden onayla
    }
    finally
    {
        chain.WriteLock.Release();
    }

    return Results.Ok(new { transaction.Id, Message = "İşlem eklendi." });
});

// Defteri baştan sona kontrol et: zincir sağlam mı?
app.MapGet("/api/audit/verify", async (AppDbContext db, HashChain chain) =>
{
    var pages = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
    string? expectedPrevious = null;   // İlk sayfanın "önceki mührü" boş olmalı

    foreach (var page in pages)
    {
        // 1) Bu sayfa gerçekten bir önceki sayfaya mı bağlı? (Arada sayfa silinmiş mi?)
        if (page.PreviousHash != expectedPrevious)
            return Results.Ok(new { Valid = false, BrokenAtAuditId = page.Id, Reason = "Zincir kopmuş: önceki sayfa silinmiş ya da değiştirilmiş." });

        // 2) İşlemin bugünkü hâliyle mührü yeniden hesapla. Tutmuyorsa işlem değiştirilmiş.
        var transaction = await db.Transactions.FindAsync(page.TransactionId);
        if (transaction == null ||
            chain.ComputeHash(transaction, page.UserId, page.Action, page.PreviousHash) != page.CurrentHash)
            return Results.Ok(new { Valid = false, BrokenAtAuditId = page.Id, Reason = $"Mühür tutmuyor: {page.TransactionId} numaralı işlem değiştirilmiş." });

        expectedPrevious = page.CurrentHash;
    }

    return Results.Ok(new { Valid = true, CheckedPages = pages.Count });
});


app.Run();


// POST isteğiyle gelen bilgiler.
record NewCustomer(string FullName, string? Email, string? Phone, string? Address);
record NewTransaction(long CustomerId, decimal Amount, string Type, string? Currency, string? Description);
record RegisterRequest(string FullName, string Email, string Password);
record LoginRequest(string? Email, string? Password);
