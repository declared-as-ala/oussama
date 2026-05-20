-- =====================================================
-- Script de création des nouvelles tables - Module Auth
-- =====================================================

USE DocDb;

-- =====================================================
-- Table Organizations
-- =====================================================
CREATE TABLE IF NOT EXISTS Organizations (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL UNIQUE,
    Code VARCHAR(50) NOT NULL UNIQUE,
    Description TEXT,
    Type VARCHAR(100),
    Address VARCHAR(500),
    Email VARCHAR(100),
    Phone VARCHAR(20),
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF',
    SubscriptionDaysRemaining INT NOT NULL DEFAULT 30,
    SubscriptionMonitorEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    LastSubscriptionDecrementAt DATETIME NULL,
    SubscriptionExpiryAlertSent BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    
    INDEX idx_code (Code),
    INDEX idx_status (Status),
    INDEX idx_created_at (CreatedAt)
);

-- =====================================================
-- Modifier la table Users existante
-- =====================================================
ALTER TABLE Users ADD COLUMN IF NOT EXISTS OrganizationId INT NULL AFTER Id;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS FirstName VARCHAR(100) NULL AFTER OrganizationId;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS LastName VARCHAR(100) NULL AFTER FirstName;
ALTER TABLE Users MODIFY COLUMN Email VARCHAR(100) NOT NULL UNIQUE;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS Function VARCHAR(100) NULL;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS Department VARCHAR(100) NULL;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS LastLoginAt DATETIME NULL;
ALTER TABLE Users ADD COLUMN IF NOT EXISTS UpdatedAt DATETIME NULL;
ALTER TABLE Users DROP COLUMN IF EXISTS Username;
ALTER TABLE Users ADD INDEX idx_organization (OrganizationId);
ALTER TABLE Users ADD INDEX idx_last_login (LastLoginAt);
ALTER TABLE Users ADD FOREIGN KEY (OrganizationId) REFERENCES Organizations(Id) ON DELETE SET NULL;

-- =====================================================
-- Table RefreshTokens
-- =====================================================
CREATE TABLE IF NOT EXISTS RefreshTokens (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    Token VARCHAR(500) NOT NULL UNIQUE,
    ExpiresAt DATETIME NOT NULL,
    IsRevoked BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    RevokedAt DATETIME NULL,
    ReplacedByToken VARCHAR(500) NULL,
    
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX idx_user (UserId),
    INDEX idx_token (Token),
    INDEX idx_expires_at (ExpiresAt),
    INDEX idx_is_revoked (IsRevoked)
);

-- =====================================================
-- Table PasswordResetTokens
-- =====================================================
CREATE TABLE IF NOT EXISTS PasswordResetTokens (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    Token VARCHAR(500) NOT NULL UNIQUE,
    ExpiresAt DATETIME NOT NULL,
    Used BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX idx_user (UserId),
    INDEX idx_token (Token),
    INDEX idx_expires_at (ExpiresAt),
    INDEX idx_used (Used)
);


-- =====================================================
-- Données initiales - Organisations de démo
-- =====================================================
INSERT INTO Organizations (Name, Code, Description, Type, Status, CreatedAt) 
VALUES (
    'Demo Organization',
    'DEMO',
    'Organisation de démonstration',
    'Test',
    'ACTIF',
    NOW()
) ON DUPLICATE KEY UPDATE Code=VALUES(Code);

-- =====================================================
-- Données initiales - Utilisateurs de démo
-- =====================================================

-- SUPER_ADMIN: superadmin@demo.local / SuperAdmin@123
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, PasswordHash, Role, IsActive, CreatedAt) 
VALUES (
    NULL,
    'Super',
    'Admin',
    'superadmin@demo.local',
    '$2a$11$8EWm4xj38Y4EkkAOaCL9..FBqQF7dGKWsP8oV5m6aPJkMvfKsJl4K',
    'SUPER_ADMIN',
    TRUE,
    NOW()
) ON DUPLICATE KEY UPDATE Email=VALUES(Email);

-- ADMIN_ORG: admin@demo.local / Admin@123
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, PasswordHash, Role, Function, IsActive, CreatedAt) 
VALUES (
    (SELECT Id FROM Organizations WHERE Code='DEMO' LIMIT 1),
    'Admin',
    'Organisation',
    'admin@demo.local',
    '$2a$11$0W4t4Dh2MyF9YLvG/X7Gle0bnYAf6KqoqWQIj1jfLkPQMy0VEQ9G.',
    'ADMIN_ORG',
    'Administrateur',
    TRUE,
    NOW()
) ON DUPLICATE KEY UPDATE Email=VALUES(Email);

-- RESPONSABLE_QUALITE: qualite@demo.local / Qualite@123
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, PasswordHash, Role, Department, IsActive, CreatedAt) 
VALUES (
    (SELECT Id FROM Organizations WHERE Code='DEMO' LIMIT 1),
    'Responsable',
    'Qualité',
    'qualite@demo.local',
    '$2a$11$mV4Zp3BK2pK9Q1RJ1sRTk.UZ8KLLPVj7Z9GHQZaZ0N6Y3r7K9Nq0C',
    'RESPONSABLE_QUALITE',
    'Qualité',
    TRUE,
    NOW()
) ON DUPLICATE KEY UPDATE Email=VALUES(Email);

-- CHEF_SERVICE: chef@demo.local / Chef@123
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, PasswordHash, Role, Department, IsActive, CreatedAt) 
VALUES (
    (SELECT Id FROM Organizations WHERE Code='DEMO' LIMIT 1),
    'Chef',
    'Service',
    'chef@demo.local',
    '$2a$11$vyC6S.L3E.h9qKBL5x6YSuY7Jx5K9pM0R8Z2H3Y0L6N9Q3X5V7W1T',
    'CHEF_SERVICE',
    'Service',
    TRUE,
    NOW()
) ON DUPLICATE KEY UPDATE Email=VALUES(Email);

-- UTILISATEUR: user@demo.local / User@123
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, PasswordHash, Role, Function, IsActive, CreatedAt) 
VALUES (
    (SELECT Id FROM Organizations WHERE Code='DEMO' LIMIT 1),
    'Utilisateur',
    'Standard',
    'user@demo.local',
    '$2a$11$FKv3wdg8eL5R2K9nM7O6p.cI1Z0B5H9e7F3D1J8L6N4Q2S9V1X3Y',
    'UTILISATEUR',
    'Utilisateur',
    TRUE,
    NOW()
) ON DUPLICATE KEY UPDATE Email=VALUES(Email);

