# SecureLedger

**English** | [Türkçe](README.tr.md)

A small ASP.NET Core Web API that shows how to store financial records securely in PostgreSQL:

- **Field encryption:** customer phone and address are encrypted with **AES-256-GCM** before they reach the database.
- **Tamper detection:** every transaction is sealed into an **HMAC-SHA256 hash chain**; one request checks the whole chain.
- **Authentication:** passwords are stored as **PBKDF2** hashes; every endpoint requires a login token.
- **No secrets in code:** connection string and keys live in **user-secrets**, never in the repository.

> This is a portfolio project, not a production banking system. See [Security limitations](#security-limitations).

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core Minimal API, .NET 10, C# |
| Front end | React 19 + Vite (plain JavaScript) |
| Database | PostgreSQL (tested with 18) |
| Data access | Entity Framework Core + Npgsql |
| Cryptography | .NET `System.Security.Cryptography` (`AesGcm`, `HMACSHA256`), ASP.NET Core `PasswordHasher` |

## How it works

```
React UI ── HTTP + Bearer token ──> ASP.NET Core API ── EF Core ──> PostgreSQL
                                   ├─ Encryption.cs   AES-256-GCM for phone / address
                                   ├─ HashChain.cs    HMAC-SHA256 seal for each transaction
                                   └─ PasswordHasher  PBKDF2 for user passwords
                                  Keys: user-secrets (never committed)
```

**Encryption.** Each value gets a fresh random 12-byte nonce. The database stores
`nonce (12 bytes) | ciphertext | tag (16 bytes)` in a `bytea` column. If even one bit is changed,
decryption fails instead of returning wrong data.

**Hash chain.** When a transaction is created, an `audit_logs` row is written in the same database
transaction:

```
current_hash = HMAC-SHA256(key, transaction data + user + action + previous_hash)
```

Each row points to the previous row's hash. `GET /api/audit/verify` walks the chain from the start and
reports the first row where a transaction was changed or a row was removed.

**Authentication.** `POST /api/auth/login` returns a bearer token (valid for 1 hour). All other
endpoints are closed by default (fallback authorization policy). The logged-in user's id is written
to the audit log.

## Project structure

```
database/schema.sql            Table definitions
SecureLedger.Api/Program.cs    Settings and all endpoints
SecureLedger.Api/Database.cs   Entities (User, Customer, Transaction, AuditLog) and DbContext
SecureLedger.Api/Encryption.cs AES-256-GCM encrypt / decrypt
SecureLedger.Api/HashChain.cs  HMAC-SHA256 hash calculation
SecureLedger.Tests/            Unit tests for encryption and hash chain (xUnit)
SecureLedger.Web/src/          React UI: login, customers, transactions, chain check
```

## API

| Method | Path | Login | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Create a user (`fullName`, `email`, `password` ≥ 8 chars) |
| POST | `/api/auth/login` | No | Returns `accessToken` |
| GET | `/api/customers` | Yes | List customers (phone / address decrypted) |
| POST | `/api/customers` | Yes | Add a customer (phone / address encrypted) |
| GET | `/api/transactions` | Yes | List transactions |
| POST | `/api/transactions` | Yes | Add a `PAYMENT` or `REFUND` and seal it into the chain |
| GET | `/api/audit/verify` | Yes | Check the hash chain |

## Run locally

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download), PostgreSQL, [Node.js 20+](https://nodejs.org), `openssl`, `jq` (for the examples).

```bash
# 1. Database
createdb secureledger_db
psql -d secureledger_db -f database/schema.sql

# 2. Secrets (stored outside the repository)
cd SecureLedger.Api
dotnet user-secrets set "ConnectionStrings:SecureLedgerDb" "Host=localhost;Port=5432;Database=secureledger_db;Username=YOUR_USER;Password=YOUR_PASSWORD"
dotnet user-secrets set "Encryption:Key" "$(openssl rand -base64 32)"
dotnet user-secrets set "HashChain:Key"  "$(openssl rand -base64 32)"

# 3. Start the API
dotnet run        # http://localhost:5059

# 4. Start the UI (second terminal, from the repository root)
cd SecureLedger.Web
npm install
npm run dev       # open http://localhost:5173
```

The UI forwards `/api` requests to the API, so no CORS setup is needed. The login token is kept
in memory only, so refreshing the page logs you out (it is never written to browser storage).

Try it:

```bash
API=http://localhost:5059

curl -X POST $API/api/auth/register -H "Content-Type: application/json" \
  -d '{"fullName":"Demo User","email":"demo@example.com","password":"choose-a-password"}'

TOKEN=$(curl -s -X POST $API/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"demo@example.com","password":"choose-a-password"}' | jq -r .accessToken)

curl -X POST $API/api/customers -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"fullName":"Test Customer","email":"customer@example.com","phone":"+90 555 000 00 00","address":"Istanbul"}'

curl -X POST $API/api/transactions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"customerId":1,"amount":250.75,"type":"PAYMENT"}'

curl $API/api/audit/verify -H "Authorization: Bearer $TOKEN"
# {"valid":true,"checkedPages":1}
```

Run the unit tests from the repository root (no database needed):

```bash
dotnet test
```

They check that encrypted data decrypts correctly, that changed bytes or a wrong key are rejected,
and that changing an amount or the previous hash changes the seal.

To see tamper detection, change an amount directly in the database
(`UPDATE transactions SET amount = 9999 WHERE id = 1;`) and call `/api/audit/verify` again.

## Threat model

| Threat | What the project does |
|---|---|
| Database dump is stolen | Phone and address are unreadable without the encryption key. Passwords are PBKDF2 hashes. |
| Someone edits a transaction in the database | The hash no longer matches; `/api/audit/verify` reports the row. |
| Someone deletes an audit row in the middle | The next row's `previous_hash` no longer matches; the chain is reported as broken. |
| Someone forges a new valid hash | Not possible without the HMAC key, which is not in the database. |
| Unauthenticated access to the API | All endpoints except register / login return `401`. |
| Password guessing (brute force) | Register / login are limited to 5 attempts per minute per IP address (`429` after that). |
| Secrets leak through GitHub | Keys and connection string are in user-secrets, not in the repository. |

## Security limitations

Known limits of this demo, stated on purpose:

- **Detects, does not prevent.** The hash chain shows that data was changed; it cannot stop or undo the change.
- **The last audit row can be deleted unnoticed**, because no later row points to it. A fix is to store the latest hash somewhere else (e.g. a separate log or signed checkpoint).
- **Anyone with the HMAC key can rewrite the whole chain.** Keys must be kept away from the database server.
- **user-secrets is for development only.** It is a plain JSON file on disk. Production would use a key vault or HSM.
- **AES-GCM does not hide length.** The ciphertext is as long as the plaintext.
- **Only phone and address are encrypted.** Customer name and email are stored in plain text.
- **Rate limiting is per IP and in memory.** An attacker with many IP addresses can still guess slowly, the counter resets when the app restarts, and behind a proxy all users may share one IP. Registration is open to anyone.
- **No key rotation yet.** `encryption_key_version` is stored, but only version 1 exists.
- **Single instance only.** The hash chain write lock is in memory.

## Roadmap

- Audit log for non-transaction events (customer created, login)
