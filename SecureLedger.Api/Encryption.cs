using System.Security.Cryptography;
using System.Text;

// Telefon ve adres gibi bilgileri AES-256-GCM ile şifreler ve çözer.
//
// Şifreli değer üç parçadan oluşur ve yan yana saklanır:
//   [ nonce: 12 bayt ][ şifreli metin ][ tag: 16 bayt ]
//   - nonce: Her seferinde yeni rastgele sayı. Aynı metin her seferinde farklı görünür.
//   - tag:   Mühür. Biri veriyi değiştirirse çözme işlemi hata verir.
public class Encryption
{
    private readonly byte[] _key;

    public Encryption(IConfiguration config)
    {
        // Anahtar user-secrets'ta durur (koda yazılmaz).
        var key = config["Encryption:Key"]
            ?? throw new Exception("Encryption:Key bulunamadı (user-secrets).");

        _key = Convert.FromBase64String(key);

        if (_key.Length != 32)
            throw new Exception("Anahtar 32 bayt (256 bit) olmalı.");
    }

    public byte[]? Encrypt(string? text)
    {
        if (text == null) return null;

        byte[] plain = Encoding.UTF8.GetBytes(text);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        // Üç parçayı birleştir: nonce + şifreli metin + tag
        return [.. nonce, .. cipher, .. tag];
    }

    public string? Decrypt(byte[]? data)
    {
        if (data == null) return null;

        // Üç parçayı geri ayır
        byte[] nonce = data[..12];       // ilk 12 bayt
        byte[] cipher = data[12..^16];   // ortası
        byte[] tag = data[^16..];        // son 16 bayt
        byte[] plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);   // Mühür bozuksa burada hata verir

        return Encoding.UTF8.GetString(plain);
    }
}
