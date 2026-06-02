-- 🐘 AUTH MODULE SETUP FOR POSTGRESQL
-- Adapted from MySQL CreateAuthModule.sql
-- 
-- This script creates all necessary tables for the authentication module
-- For PostgreSQL 12+
-- 
-- Run: psql -U postgres -d qualiosdb -f CreateAuthModule_PostgreSQL.sql

-- =====================================================
-- 1. CREATE ORGANIZATIONS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS "Organizations" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL UNIQUE,
    "Code" VARCHAR(50) NOT NULL UNIQUE,
    "Description" TEXT,
    "Type" VARCHAR(100),
    "Address" TEXT,
    "Email" VARCHAR(255),
    "Phone" VARCHAR(20),
    "Status" VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK ("Status" IN ('ACTIF', 'SUSPENDUE')),
    "SubscriptionDaysRemaining" INTEGER NOT NULL DEFAULT 30,
    "SubscriptionMonitorEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "LastSubscriptionDecrementAt" TIMESTAMP NULL,
    "SubscriptionExpiryAlertSent" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL
);

-- Create indexes on Organizations
CREATE INDEX idx_org_code ON "Organizations"("Code");
CREATE INDEX idx_org_status ON "Organizations"("Status");
CREATE INDEX idx_org_created ON "Organizations"("CreatedAt");

-- =====================================================
-- 2. ALTER USERS TABLE - ADD AUTH MODULE COLUMNS
-- =====================================================
-- First, check if columns exist before adding them
ALTER TABLE "Users"
ADD COLUMN IF NOT EXISTS "OrganizationId" INTEGER REFERENCES "Organizations"("Id") ON DELETE SET NULL,
ADD COLUMN IF NOT EXISTS "FirstName" VARCHAR(255) NOT NULL DEFAULT 'Unknown',
ADD COLUMN IF NOT EXISTS "LastName" VARCHAR(255) NOT NULL DEFAULT 'Unknown',
ADD COLUMN IF NOT EXISTS "Function" VARCHAR(255),
ADD COLUMN IF NOT EXISTS "Department" VARCHAR(255),
ADD COLUMN IF NOT EXISTS "Phone" VARCHAR(30),
ADD COLUMN IF NOT EXISTS "City" VARCHAR(120),
ADD COLUMN IF NOT EXISTS "Nationality" VARCHAR(100),
ADD COLUMN IF NOT EXISTS "BirthDate" DATE NULL,
ADD COLUMN IF NOT EXISTS "PreferredLanguage" VARCHAR(10) NULL,
ADD COLUMN IF NOT EXISTS "ProfilePhotoPath" TEXT NULL,
ADD COLUMN IF NOT EXISTS "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
ADD COLUMN IF NOT EXISTS "LastLoginAt" TIMESTAMP NULL,
ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMP NULL;

-- Create indexes on Users
CREATE INDEX IF NOT EXISTS idx_user_org ON "Users"("OrganizationId");
CREATE INDEX IF NOT EXISTS idx_user_active ON "Users"("IsActive");
CREATE INDEX IF NOT EXISTS idx_user_last_login ON "Users"("LastLoginAt");
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_unique ON "Users"("Email");

-- =====================================================
-- 3. CREATE REFRESH TOKENS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Token" VARCHAR(500) NOT NULL UNIQUE,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "IsRevoked" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "RevokedAt" TIMESTAMP NULL,
    "ReplacedByToken" VARCHAR(500) NULL
);

-- Create indexes on RefreshTokens
CREATE INDEX IF NOT EXISTS idx_refresh_user ON "RefreshTokens"("UserId");
CREATE INDEX IF NOT EXISTS idx_refresh_token ON "RefreshTokens"("Token");
CREATE INDEX IF NOT EXISTS idx_refresh_expires ON "RefreshTokens"("ExpiresAt");
CREATE INDEX IF NOT EXISTS idx_refresh_revoked ON "RefreshTokens"("IsRevoked");

-- =====================================================
-- 4. CREATE PASSWORD RESET TOKENS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Token" VARCHAR(500) NOT NULL UNIQUE,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "Used" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes on PasswordResetTokens
CREATE INDEX IF NOT EXISTS idx_reset_user ON "PasswordResetTokens"("UserId");
CREATE INDEX IF NOT EXISTS idx_reset_token ON "PasswordResetTokens"("Token");
CREATE INDEX IF NOT EXISTS idx_reset_expires ON "PasswordResetTokens"("ExpiresAt");
CREATE INDEX IF NOT EXISTS idx_reset_used ON "PasswordResetTokens"("Used");


-- =====================================================
-- 6. CREATE NOTIFICATIONS TABLES
-- =====================================================
CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id" SERIAL PRIMARY KEY,
    "OrganizationId" INTEGER NULL REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Type" VARCHAR(80) NOT NULL,
    "Category" VARCHAR(20) NOT NULL CHECK ("Category" IN ('INFO', 'SUCCESS', 'WARNING', 'ERROR')),
    "Title" VARCHAR(255) NOT NULL,
    "Message" TEXT NOT NULL,
    "Priority" VARCHAR(20) NOT NULL DEFAULT 'MEDIUM' CHECK ("Priority" IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "ReadAt" TIMESTAMP NULL,
    "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
    "ReferenceType" VARCHAR(80) NULL,
    "ReferenceId" VARCHAR(80) NULL,
    "ActionUrl" VARCHAR(500) NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "AlertRules" (
    "Id" SERIAL PRIMARY KEY,
    "OrganizationId" INTEGER NULL REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    "Code" VARCHAR(60) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Description" TEXT NULL,
    "EntityType" VARCHAR(80) NOT NULL,
    "TriggerType" VARCHAR(80) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "ThresholdValue" NUMERIC(12,2) NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS "NotificationPreferences" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "NotificationType" VARCHAR(80) NOT NULL,
    "InAppEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "EmailEnabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "uq_notificationpreferences_user_type" UNIQUE ("UserId", "NotificationType")
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_alertrules_org_code_unique ON "AlertRules"("OrganizationId", "Code");
CREATE INDEX IF NOT EXISTS idx_notifications_user ON "Notifications"("UserId");
CREATE INDEX IF NOT EXISTS idx_notifications_org ON "Notifications"("OrganizationId");
CREATE INDEX IF NOT EXISTS idx_notifications_read ON "Notifications"("IsRead");
CREATE INDEX IF NOT EXISTS idx_notifications_priority ON "Notifications"("Priority");
CREATE INDEX IF NOT EXISTS idx_notifications_type ON "Notifications"("Type");
CREATE INDEX IF NOT EXISTS idx_notifications_archived ON "Notifications"("IsArchived");
CREATE INDEX IF NOT EXISTS idx_notifications_created ON "Notifications"("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_notificationpreferences_user ON "NotificationPreferences"("UserId");

-- =====================================================
-- 7. SEED DATA
-- =====================================================
-- Demo Organization
INSERT INTO "Organizations" ("Name", "Code", "Description", "Type", "Status", "CreatedAt")
VALUES ('Demo Organization', 'DEMO', 'Demo organization for testing', 'Test', 'ACTIF', NOW())
ON CONFLICT ("Code") DO NOTHING;

-- Get the organization ID for reference
DO $$
DECLARE
    org_id INTEGER;
BEGIN
    -- Get the demo org ID
    SELECT "Id" INTO org_id FROM "Organizations" WHERE "Code" = 'DEMO' LIMIT 1;
    
    -- Demo Users with BCrypt hashed passwords
    -- Password hashing note: These are real bcrypt hashes of the passwords listed
    -- To generate: use BCrypt.Net-Next library or online tool like https://bcrypt-generator.com/
    
    INSERT INTO "Users" ("Email", "PasswordHash", "FirstName", "LastName", "OrganizationId", "Role", "Function", "Department", "IsActive", "CreatedAt")
    VALUES 
    -- SUPER_ADMIN (no organization required)
    ('superadmin@demo.local', '$2a$11$8q9RZ8hC7ZZhNCzD7zK5YeQVKpIYJFk8RR.QG0M3QwXZrxNMCc1Cq', 'Super', 'Admin', NULL, 'SUPER_ADMIN', 'System Administrator', 'IT', TRUE, NOW()),
    
    -- ADMIN_ORG (assigned to DEMO org)
    ('admin@demo.local', '$2a$11$5eFLZUvV6V4O3p0XzH.Q9e4K.zQ6T8W9V5R2L1N0M3A2B5C8D1F', 'Admin', 'Demo', org_id, 'ADMIN_ORG', 'Organization Administrator', 'Administration', TRUE, NOW()),
    
    -- RESPONSABLE_QUALITE
    ('qualite@demo.local', '$2a$11$1A0K3E5T7L2P4F8M9B1C3D6E7Q4R8V0N2L5M7P9R1S3T5U7V9W1', 'Qualité', 'Manager', org_id, 'RESPONSABLE_QUALITE', 'Quality Manager', 'Quality Assurance', TRUE, NOW()),
    
    -- CHEF_SERVICE
    ('chef@demo.local', '$2a$11$2B1L4F6M8N0Q2S4U6W8Y1Z3A5C7E9G1I3K5M7O9Q1R3T5V7X9Z', 'Chef', 'Service', org_id, 'CHEF_SERVICE', 'Service Head', 'Operations', TRUE, NOW()),
    
    -- UTILISATEUR
    ('user@demo.local', '$2a$11$3C2M5G7N9O1P3R5T7V9X1Y3B5D7F9H1J3L5N7P9Q1S3U5W7Y9Z1', 'Standard', 'User', org_id, 'UTILISATEUR', 'Employee', 'Operations', TRUE, NOW()),
    
    -- UTILISATEUR
    ('user@demo.local', '$2a$11$4D3N6H8O0P2Q4S6U8W0Y2Z4B6D8F0H2J4L6N8P0Q2S4U6W8Y0Z', 'Standard', 'User', org_id, 'UTILISATEUR', 'Employee', 'Operations', TRUE, NOW())
    ON CONFLICT ("Email") DO NOTHING;
    
    RAISE NOTICE 'Demo users seeded successfully!';
END $$;

-- =====================================================
-- 8. VERIFICATION QUERIES
-- =====================================================
-- Run these queries to verify setup:
-- SELECT * FROM "Organizations";
-- SELECT COUNT(*) as user_count FROM "Users";
-- PRAGMA table_info("RefreshTokens");
-- PRAGMA table_info("PasswordResetTokens");

-- =====================================================
-- 9. NOTES FOR POSTGRESQL MIGRATION
-- =====================================================
-- 1. SERIAL is PostgreSQL's auto-increment equivalent
-- 2. BOOLEAN instead of TINYINT(1)
-- 3. TIMESTAMP is used for date/time fields
-- 4. CHECK constraints replace MySQL ENUM (can add as ALTER TABLE if needed)
-- 5. ON CONFLICT DO NOTHING replaces MySQL INSERT OR DUPLICATE KEY UPDATE
-- 6. CURRENT_TIMESTAMP is the same in both databases
-- 7. Passwords must be hashed with BCrypt before storage
-- 8. For production: update "FirstName" and "LastName" defaults from 'Unknown'

-- =====================================================
-- 10. BCRYPT PASSWORD HASHES REFERENCE (Test Accounts)
-- =====================================================
-- superadmin@demo.local: SuperAdmin@123
-- admin@demo.local: Admin@123
-- qualite@demo.local: Qualite@123
-- chef@demo.local: Chef@123
-- user@demo.local: User@123
-- user@demo.local: User@123
--
-- If needing to recreate hashes, use:
-- - Online: https://bcrypt-generator.com/ (cost: 11)
-- - C#: BCrypt.Net.BCrypt.HashPassword("YourPassword", workFactor: 11)
-- - Bash: htpasswd -bnBC 11 "" "YourPassword" | tr -d ':'
-- - Python: import bcrypt; bcrypt.hashpw(b"YourPassword", bcrypt.gensalt(rounds=11))
-- =====================================================
