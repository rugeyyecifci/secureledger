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


app.Run();


// POST isteğiyle gelen bilgiler.
record NewCustomer(string FullName, string? Email, string? Phone, string? Address);
