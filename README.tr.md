# SecureLedger

[English](README.md) | **Türkçe**

Finansal kayıtları PostgreSQL'de güvenli şekilde saklamayı gösteren küçük bir ASP.NET Core Web API:

- **Alan şifreleme:** Müşterinin telefonu ve adresi, veritabanına gitmeden önce **AES-256-GCM** ile şifrelenir.
- **Değişiklik tespiti:** Her işlem bir **HMAC-SHA256 hash zincirine** mühürlenir; tek bir istekle bütün zincir kontrol edilir.
- **Kimlik doğrulama:** Şifreler **PBKDF2** hash'i olarak saklanır; her endpoint giriş anahtarı (token) ister.
- **Kodda sır yok:** Bağlantı bilgisi ve anahtarlar **user-secrets**'ta durur, repoya hiç girmez.

> Bu bir portföy projesidir, gerçek bir bankacılık sistemi değildir. Bkz. [Güvenlik sınırları](#güvenlik-sınırları).

## Teknolojiler

| Katman | Teknoloji |
|---|---|
| API | ASP.NET Core Minimal API, .NET 10, C# |
| Arayüz | React 19 + Vite (düz JavaScript) |
| Veritabanı | PostgreSQL (18 ile test edildi) |
| Veri erişimi | Entity Framework Core + Npgsql |
| Kriptografi | .NET `System.Security.Cryptography` (`AesGcm`, `HMACSHA256`), ASP.NET Core `PasswordHasher` |

## Nasıl çalışır?

```
React arayüz ── HTTP + Bearer token ──> ASP.NET Core API ── EF Core ──> PostgreSQL
                                    ├─ Encryption.cs   Telefon / adres için AES-256-GCM
                                    ├─ HashChain.cs    Her işlem için HMAC-SHA256 mührü
                                    └─ PasswordHasher  Kullanıcı şifreleri için PBKDF2
                                   Anahtarlar: user-secrets (repoya girmez)
```

**Şifreleme.** Her değer için yeni ve rastgele 12 baytlık bir nonce üretilir. Veritabanında `bytea`
sütununa `nonce (12 bayt) | şifreli veri | tag (16 bayt)` yazılır. Tek bir bit bile değişirse, çözme
işlemi yanlış veri döndürmek yerine hata verir.

**Hash zinciri.** Bir işlem eklendiğinde, aynı veritabanı transaction'ı içinde `audit_logs` tablosuna
bir satır yazılır:

```
current_hash = HMAC-SHA256(anahtar, işlem bilgileri + kullanıcı + eylem + previous_hash)
```

Her satır bir önceki satırın mührüne bağlıdır. `GET /api/audit/verify` zinciri baştan sona dolaşır ve
bir işlemin değiştirildiği ya da bir satırın silindiği ilk yeri bildirir.

**Kimlik doğrulama.** `POST /api/auth/login` bir bearer token döndürür (1 saat geçerli). Diğer bütün
endpoint'ler varsayılan olarak kapalıdır (fallback authorization policy). Giriş yapan kullanıcının
id'si audit log'a yazılır.

## Proje yapısı

```
database/schema.sql            Tablo tanımları
SecureLedger.Api/Program.cs    Ayarlar ve bütün endpoint'ler
SecureLedger.Api/Database.cs   Entity'ler (User, Customer, Transaction, AuditLog) ve DbContext
SecureLedger.Api/Encryption.cs AES-256-GCM şifreleme / çözme
SecureLedger.Api/HashChain.cs  HMAC-SHA256 mühür hesaplama
SecureLedger.Tests/            Şifreleme ve hash zinciri için birim testleri (xUnit)
SecureLedger.Web/src/          React arayüz: giriş, müşteriler, işlemler, zincir kontrolü
```

## API

| Metot | Yol | Giriş | Açıklama |
|---|---|---|---|
| POST | `/api/auth/register` | Hayır | Kullanıcı oluştur (`fullName`, `email`, en az 8 karakter `password`) |
| POST | `/api/auth/login` | Hayır | `accessToken` döndürür |
| GET | `/api/customers` | Evet | Müşterileri listele (telefon / adres çözülmüş) |
| POST | `/api/customers` | Evet | Müşteri ekle (telefon / adres şifrelenir) |
| GET | `/api/transactions` | Evet | İşlemleri listele |
| POST | `/api/transactions` | Evet | `PAYMENT` veya `REFUND` ekle ve zincire mühürle |
| GET | `/api/audit/verify` | Evet | Hash zincirini kontrol et |

## Yerelde çalıştırma

Gerekenler: [.NET SDK 10](https://dotnet.microsoft.com/download), PostgreSQL, [Node.js 20+](https://nodejs.org), `openssl`, `jq` (örnekler için).

```bash
# 1. Veritabanı
createdb secureledger_db
psql -d secureledger_db -f database/schema.sql

# 2. Sırlar (repo dışında saklanır)
cd SecureLedger.Api
dotnet user-secrets set "ConnectionStrings:SecureLedgerDb" "Host=localhost;Port=5432;Database=secureledger_db;Username=KULLANICI;Password=SIFRE"
dotnet user-secrets set "Encryption:Key" "$(openssl rand -base64 32)"
dotnet user-secrets set "HashChain:Key"  "$(openssl rand -base64 32)"

# 3. API'yi başlat
dotnet run        # http://localhost:5059

# 4. Arayüzü başlat (ikinci terminalde, repo ana klasöründen)
cd SecureLedger.Web
npm install
npm run dev       # http://localhost:5173 adresini açın
```

Arayüz `/api` isteklerini API'ye iletir, bu yüzden CORS ayarı gerekmez. Giriş anahtarı sadece
bellekte tutulur; sayfa yenilenince tekrar giriş gerekir (tarayıcı deposuna hiç yazılmaz).

Deneyin:

```bash
API=http://localhost:5059

curl -X POST $API/api/auth/register -H "Content-Type: application/json" \
  -d '{"fullName":"Demo Kullanıcı","email":"demo@example.com","password":"bir-sifre-secin"}'

TOKEN=$(curl -s -X POST $API/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"demo@example.com","password":"bir-sifre-secin"}' | jq -r .accessToken)

curl -X POST $API/api/customers -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"fullName":"Test Müşteri","email":"musteri@example.com","phone":"+90 555 000 00 00","address":"İstanbul"}'

curl -X POST $API/api/transactions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"customerId":1,"amount":250.75,"type":"PAYMENT"}'

curl $API/api/audit/verify -H "Authorization: Bearer $TOKEN"
# {"valid":true,"checkedPages":1}
```

Birim testlerini repo ana klasöründen çalıştırın (veritabanı gerekmez):

```bash
dotnet test
```

Testler şunları kontrol eder: Şifreli veri doğru çözülüyor mu, değiştirilmiş veri veya yanlış anahtar
reddediliyor mu, tutar ya da önceki mühür değişince mühür değişiyor mu.

Değişiklik tespitini görmek için veritabanında bir tutarı doğrudan değiştirin
(`UPDATE transactions SET amount = 9999 WHERE id = 1;`) ve `/api/audit/verify` isteğini tekrar gönderin.

## Tehdit modeli

| Tehdit | Proje ne yapıyor |
|---|---|
| Veritabanının kopyası çalınır | Telefon ve adres, şifreleme anahtarı olmadan okunamaz. Şifreler PBKDF2 hash'idir. |
| Biri veritabanında bir işlemi değiştirir | Mühür artık tutmaz; `/api/audit/verify` o satırı bildirir. |
| Biri ortadan bir audit satırı siler | Sonraki satırın `previous_hash` değeri artık tutmaz; zincir kopuk olarak bildirilir. |
| Biri yeni ve geçerli bir mühür üretmeye çalışır | Veritabanında bulunmayan HMAC anahtarı olmadan mümkün değil. |
| API'ye girişsiz erişim | Kayıt ve giriş dışındaki bütün endpoint'ler `401` döndürür. |
| Şifre tahmini (brute force) | Kayıt ve giriş, IP adresi başına dakikada 5 denemeyle sınırlıdır (sonrası `429`). |
| Sırların GitHub'a sızması | Anahtarlar ve bağlantı bilgisi user-secrets'ta, repoda değil. |

## Güvenlik sınırları

Bu demonun bilinen sınırları, bilerek belirtilmiştir:

- **Tespit eder, engellemez.** Hash zinciri verinin değiştirildiğini gösterir; değişikliği durduramaz veya geri alamaz.
- **Son audit satırı fark edilmeden silinebilir**, çünkü ona bağlı sonraki bir satır yoktur. Çözümü, son mührü başka bir yerde de saklamaktır (ör. ayrı bir log veya imzalı kontrol noktası).
- **HMAC anahtarına sahip biri bütün zinciri yeniden yazabilir.** Anahtarlar veritabanı sunucusundan uzak tutulmalıdır.
- **user-secrets sadece geliştirme içindir.** Diskte düz bir JSON dosyasıdır. Gerçek sistemde key vault veya HSM kullanılır.
- **AES-GCM uzunluğu gizlemez.** Şifreli veri, düz metinle aynı uzunluktadır.
- **Sadece telefon ve adres şifrelidir.** Müşteri adı ve e-postası düz metin saklanır.
- **Deneme sınırı IP başınadır ve bellekte tutulur.** Çok sayıda IP adresi olan biri yine yavaşça deneyebilir, uygulama yeniden başlayınca sayaç sıfırlanır, proxy arkasında bütün kullanıcılar aynı IP'yi paylaşabilir. Kayıt herkese açıktır.
- **Anahtar değişimi (key rotation) henüz yok.** `encryption_key_version` saklanıyor ama sadece 1. sürüm var.
- **Tek sunucu varsayımı.** Hash zinciri yazma kilidi bellekte tutuluyor.

## Yol haritası

- İşlem dışı olaylar için audit log (müşteri eklendi, giriş yapıldı)
