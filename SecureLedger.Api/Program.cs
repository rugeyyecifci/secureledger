using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1) AYARLAR ----------

// Veritabanı bağlantısı. Bilgi user-secrets'tan okunur (koda yazılmaz).
var connectionString = builder.Configuration.GetConnectionString("SecureLedgerDb");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Şifreleme servisi. Uygulama boyunca tek bir tane kullanılır.
builder.Services.AddSingleton<Encryption>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();   // API dokümanı: /openapi/v1.json
}

app.UseHttpsRedirection();


// ---------- 2) ENDPOINT'LER ----------

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

// İşlem ekle (ödeme veya iade).
app.MapPost("/api/transactions", async (NewTransaction input, AppDbContext db) =>
{
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

    db.Transactions.Add(transaction);
    await db.SaveChangesAsync();   // Veritabanına yaz

    return Results.Ok(new { transaction.Id, Message = "İşlem eklendi." });
});


app.Run();


// POST isteğiyle gelen bilgiler.
record NewCustomer(string FullName, string? Email, string? Phone, string? Address);
record NewTransaction(long CustomerId, decimal Amount, string Type, string? Currency, string? Description);
