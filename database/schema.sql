-- SecureLedger database schema (PostgreSQL)
-- Create the database first:  CREATE DATABASE secureledger_db;
-- Then run this file:         psql -d secureledger_db -f database/schema.sql

-- People who log in to the API.
CREATE TABLE users (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name     varchar(100) NOT NULL,
    email         varchar(255) NOT NULL UNIQUE,
    password_hash text         NOT NULL,              -- PBKDF2 hash, never the password
    created_at    timestamptz  NOT NULL DEFAULT now()
);

-- Customers. Phone and address are stored encrypted (AES-256-GCM: nonce | ciphertext | tag).
CREATE TABLE customers (
    id                     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name              varchar(100) NOT NULL,
    email                  varchar(255) UNIQUE,
    encrypted_phone        bytea,
    encrypted_address      bytea,
    encryption_key_version integer      NOT NULL DEFAULT 1,
    created_at             timestamptz  NOT NULL DEFAULT now()
);

-- Payments and refunds.
CREATE TABLE transactions (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_id      bigint        NOT NULL,
    amount           numeric(15,2) NOT NULL,
    currency         varchar(3)    NOT NULL DEFAULT 'TRY',
    transaction_type varchar(20)   NOT NULL,
    description      text,
    created_at       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT fk_transaction_customer FOREIGN KEY (customer_id) REFERENCES customers(id),
    CONSTRAINT chk_amount_positive     CHECK (amount > 0),
    CONSTRAINT chk_transaction_type    CHECK (transaction_type IN ('PAYMENT', 'REFUND'))
);

-- Audit log + hash chain. Each row's current_hash = HMAC-SHA256(row data + previous_hash).
CREATE TABLE audit_logs (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    transaction_id bigint      NOT NULL,
    user_id        bigint      NOT NULL,
    action         varchar(30) NOT NULL,
    previous_hash  varchar(64),                       -- NULL only for the first row
    current_hash   varchar(64) NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_audit_transaction FOREIGN KEY (transaction_id) REFERENCES transactions(id),
    CONSTRAINT fk_audit_user        FOREIGN KEY (user_id)        REFERENCES users(id),
    CONSTRAINT chk_audit_action     CHECK (action IN ('CREATE', 'UPDATE', 'DELETE'))
);
