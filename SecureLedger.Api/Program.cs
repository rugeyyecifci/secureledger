// ============================================================
// 1) UYGULAMAYI KURMA (BUILDER) AŞAMASI
// ============================================================

// WebApplication.CreateBuilder: Web uygulamasını kurmak için bir "builder" (inşa edici) oluşturur.
// - args: Komut satırından gelen parametreler (örn. --urls=http://localhost:5000)
// - Arka planda şunları otomatik yapar:
//     * appsettings.json ve appsettings.{Environment}.json dosyalarını okur
//     * Ortam değişkenlerini (environment variables) yükler
//     * Loglama (logging) altyapısını kurar
//     * Kestrel web sunucusunu hazırlar
var builder = WebApplication.CreateBuilder(args);

// builder.Services: "Dependency Injection" (Bağımlılık Enjeksiyonu) konteyneri.
// Uygulamanın kullanacağı servisler buraya kaydedilir. Sonra ihtiyaç duyulan yerde
// bu servisler otomatik olarak verilir.
//
// AddOpenApi(): API'nin OpenAPI (eski adıyla Swagger) dokümanını üretecek servisi ekler.
// Bu doküman API'deki tüm endpoint'leri, parametreleri ve dönüş tiplerini JSON olarak tarif eder.
// Daha fazla bilgi: https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Build(): Bütün ayarlar ve servisler tamamlandı, artık gerçek uygulama nesnesini oluştur.
// Bu satırdan sonra builder.Services'e yeni servis EKLENEMEZ.
var app = builder.Build();


// ============================================================
// 2) HTTP İSTEK HATTINI (MIDDLEWARE PIPELINE) YAPILANDIRMA
// ============================================================
// Her gelen HTTP isteği, burada sırayla eklenen "middleware"lerden geçer.
// Sıralama önemlidir!

// Uygulama "Development" (geliştirme) ortamında mı çalışıyor?
// Ortam bilgisi ASPNETCORE_ENVIRONMENT değişkeninden gelir
// (Properties/launchSettings.json içinde "Development" olarak ayarlı).
if (app.Environment.IsDevelopment())
{
    // OpenAPI dokümanını /openapi/v1.json adresinde yayınlar.
    // Sadece geliştirme ortamında açılır; canlı ortamda (Production)
    // API yapınızı dışarıya göstermemek için kapalı kalır.
    app.MapOpenApi();
}

// HTTP ile gelen istekleri otomatik olarak HTTPS'e yönlendirir.
// Örn: http://site.com/x  -->  https://site.com/x
// Güvenlik için önemlidir, özellikle finans ("Ledger") uygulaması için.
app.UseHttpsRedirection();


// ============================================================
// 3) ÖRNEK VERİ
// ============================================================

// Hava durumu açıklamalarını tutan string dizisi.
// "new[] { ... }" yazımında derleyici tipi kendisi bulur: string[]
// Sıralama soğuktan sıcağa doğru: Dondurucu, Keskin soğuk, Serin, ... Kavurucu
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};


// ============================================================
// 4) ENDPOINT (UÇ NOKTA) TANIMLAMA
// ============================================================

// MapGet: "/weatherforecast" adresine gelen HTTP GET isteklerini karşılar.
// İkinci parametre bir "lambda" fonksiyonudur: () => { ... }
// İstek geldiğinde bu fonksiyon çalışır ve döndürdüğü değer otomatik olarak
// JSON'a çevrilip istemciye gönderilir.
app.MapGet("/weatherforecast", () =>
{
    // Enumerable.Range(1, 5): 1, 2, 3, 4, 5 sayılarını üretir (5 günlük tahmin).
    // .Select(index => ...): Her sayı için bir WeatherForecast nesnesi oluşturur (LINQ).
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            // Tarih: Bugünden "index" gün sonrası (yarın, öbür gün, ...).
            // DateOnly: Sadece tarih tutar, saat bilgisi yoktur.
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),

            // Sıcaklık: -20 ile 54 arasında rastgele bir sayı (üst sınır 55 dahil değil).
            // Random.Shared: Thread-safe (çoklu iş parçacığında güvenli) ortak Random nesnesi.
            Random.Shared.Next(-20, 55),

            // Açıklama: summaries dizisinden rastgele bir eleman seçer.
            // Not: Sıcaklıkla uyumlu değildir, -15°C için "Hot" gelebilir. Sadece örnek veri.
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();   // LINQ sonucunu diziye çevirir (sorguyu hemen çalıştırır).

    // Diziyi döndür. ASP.NET Core bunu otomatik olarak JSON'a çevirir ve
    // 200 OK durum koduyla gönderir.
    return forecast;
})
// Endpoint'e bir isim verir. Bu isim:
//  - OpenAPI dokümanında "operationId" olarak görünür
//  - Kod içinde link üretirken (LinkGenerator) bu endpoint'e isimle başvurmayı sağlar
.WithName("GetWeatherForecast");


// ============================================================
// 5) UYGULAMAYI ÇALIŞTIRMA
// ============================================================

// Web sunucusunu başlatır ve gelen istekleri dinlemeye başlar.
// Program kapatılana kadar (Ctrl+C) bu satırda bekler.
app.Run();


// ============================================================
// 6) VERİ MODELİ
// ============================================================

// "record": Veri taşımak için kullanılan özel bir tip (C# 9 ile geldi).
// Özellikleri:
//  - Parantez içindeki parametreler otomatik olarak sadece okunabilir (get; init;) property olur
//  - İki record, içindeki değerler aynıysa "eşit" sayılır (değer bazlı eşitlik)
//  - Otomatik olarak düzgün bir ToString() metodu üretilir
//
// string? : Soru işareti, Summary'nin null olabileceğini belirtir (Nullable reference types).
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    // Hesaplanan property: Celsius'u Fahrenheit'a çevirir.
    // "=>" ifadesi burada "expression-bodied property"dir: her okunduğunda yeniden hesaplanır.
    // Formül: F = 32 + C / 0.5556  (0.5556 ≈ 5/9, yani C × 9/5 ile aynı şey)
    // (int) ile ondalık kısım atılır.
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
