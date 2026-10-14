using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

// HashChain.cs (HMAC-SHA256 mühür) için testler.
public class HashChainTests
{
    private static HashChain CreateChain()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HashChain:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            })
            .Build();

        return new HashChain(config);
    }

    // Örnek bir işlem
    private static Transaction SampleTransaction() => new()
    {
        Id = 1,
        CustomerId = 1,
        Amount = 250.75m,
        Currency = "TRY",
        Type = "PAYMENT",
        Description = "Test",
        CreatedAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Ayni_islem_her_zaman_ayni_muhru_verir()
    {
        var chain = CreateChain();

        var hash1 = chain.ComputeHash(SampleTransaction(), userId: 1, "CREATE", previousHash: null);
        var hash2 = chain.ComputeHash(SampleTransaction(), userId: 1, "CREATE", previousHash: null);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);   // SHA-256 = 32 bayt = 64 hex karakter
    }

    [Fact]
    public void Tutar_degisirse_muhur_degisir()
    {
        var chain = CreateChain();
        var original = SampleTransaction();
        var tampered = SampleTransaction();
        tampered.Amount = 2507.50m;

        Assert.NotEqual(
            chain.ComputeHash(original, 1, "CREATE", null),
            chain.ComputeHash(tampered, 1, "CREATE", null));
    }

    [Fact]
    public void Onceki_muhur_degisirse_muhur_degisir()
    {
        var chain = CreateChain();

        // Zincirin temeli: her mühür bir öncekine bağlı.
        Assert.NotEqual(
            chain.ComputeHash(SampleTransaction(), 1, "CREATE", previousHash: "aaa"),
            chain.ComputeHash(SampleTransaction(), 1, "CREATE", previousHash: "bbb"));
    }

    [Fact]
    public void Farkli_anahtar_farkli_muhur_verir()
    {
        // Anahtarı bilmeyen biri aynı mührü üretemez.
        Assert.NotEqual(
            CreateChain().ComputeHash(SampleTransaction(), 1, "CREATE", null),
            CreateChain().ComputeHash(SampleTransaction(), 1, "CREATE", null));
    }
}
