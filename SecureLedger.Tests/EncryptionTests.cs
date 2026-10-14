using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

// Encryption.cs (AES-256-GCM) için testler.
public class EncryptionTests
{
    // Her test için rastgele bir anahtarla yeni bir kilitçi oluşturur.
    private static Encryption CreateEncryption(string? key = null)
    {
        key ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = key })
            .Build();

        return new Encryption(config);
    }

    [Fact]
    public void Kilitlenen_metin_geri_acilinca_ayni_olur()
    {
        var enc = CreateEncryption();

        var encrypted = enc.Encrypt("+90 555 000 00 00 – Ümraniye, İstanbul");

        Assert.Equal("+90 555 000 00 00 – Ümraniye, İstanbul", enc.Decrypt(encrypted));
    }

    [Fact]
    public void Ayni_metin_her_seferinde_farkli_kilitlenir()
    {
        var enc = CreateEncryption();

        // Rastgele nonce sayesinde aynı telefon iki kez kilitlenince farklı görünür.
        Assert.NotEqual(enc.Encrypt("+90 555 000 00 00"), enc.Encrypt("+90 555 000 00 00"));
    }

    [Fact]
    public void Kilitli_veride_tek_bayt_degisirse_acilmaz()
    {
        var enc = CreateEncryption();
        var encrypted = enc.Encrypt("+90 555 000 00 00")!;

        encrypted[15] ^= 1;   // Şifreli verinin ortasındaki bir bitini değiştir

        // Mühür (tag) tutmaz: bozuk veri döndürmek yerine hata verir.
        Assert.ThrowsAny<CryptographicException>(() => enc.Decrypt(encrypted));
    }

    [Fact]
    public void Yanlis_anahtarla_acilmaz()
    {
        var encrypted = CreateEncryption().Encrypt("+90 555 000 00 00");

        Assert.ThrowsAny<CryptographicException>(() => CreateEncryption().Decrypt(encrypted));
    }

    [Fact]
    public void Anahtar_32_bayt_degilse_baslamaz()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Assert.Throws<Exception>(() => CreateEncryption(shortKey));
    }
}
