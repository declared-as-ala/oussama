-- =====================================================
-- Script de création de la base de données DocApi
-- =====================================================

-- Création de la base de données
CREATE DATABASE IF NOT EXISTS DocDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE DocDb;

-- =====================================================
-- Table Users (créée en premier car référencée par les autres)
-- =====================================================
CREATE TABLE IF NOT EXISTS Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL DEFAULT 'User',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    
    INDEX idx_username (Username),
    INDEX idx_email (Email),
    INDEX idx_role (Role)
);

-- =====================================================
-- Table TypeDocument
-- =====================================================
CREATE TABLE IF NOT EXISTS TypeDocument (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    Description TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    CreatedByUserId INT NOT NULL,
    UpdatedByUserId INT NULL,
    
    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id) ON DELETE RESTRICT,
    FOREIGN KEY (UpdatedByUserId) REFERENCES Users(Id) ON DELETE SET NULL,
    INDEX idx_name (Name),
    INDEX idx_created_by (CreatedByUserId),
    INDEX idx_updated_by (UpdatedByUserId)
);

-- =====================================================
-- Table Document
-- =====================================================
CREATE TABLE IF NOT EXISTS Document (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Title VARCHAR(255) NOT NULL,
    FilePath VARCHAR(500) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL,
    TypeDocumentId INT NOT NULL,
    CreatedByUserId INT NOT NULL,
    UpdatedByUserId INT NULL,
    
    FOREIGN KEY (TypeDocumentId) REFERENCES TypeDocument(Id) ON DELETE RESTRICT,
    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id) ON DELETE RESTRICT,
    FOREIGN KEY (UpdatedByUserId) REFERENCES Users(Id) ON DELETE SET NULL,
    INDEX idx_title (Title),
    INDEX idx_created_at (CreatedAt),
    INDEX idx_type_document (TypeDocumentId),
    INDEX idx_created_by (CreatedByUserId),
    INDEX idx_updated_by (UpdatedByUserId)
);

-- =====================================================
-- Utilisateurs par défaut avec mots de passe hashés
-- =====================================================

-- Administrateur principal
-- Mot de passe réel: admin123
INSERT INTO Users (Username, Email, PasswordHash, Role, CreatedAt, IsActive) 
VALUES (
    'admin', 
    'admin@docapi.com', 
    '$2a$11$gxHA2902.ZSkiq.ffzxOKefbk8vhYL4pbn.3VXUaPqRx7akHD1B/a', 
    'Admin', 
    NOW(), 
    TRUE
) ON DUPLICATE KEY UPDATE Username = VALUES(Username);

-- Utilisateur standard de test
-- Mot de passe réel: user123
INSERT INTO Users (Username, Email, PasswordHash, Role, CreatedAt, IsActive) 
VALUES (
    'user1', 
    'user1@docapi.com', 
    '$2a$11$kfJmjzRGyyStto7srRpIDuZV5FOSou5ho.ZkRez5LyWRziiwpI/T.', 
    'User', 
    NOW(), 
    TRUE
) ON DUPLICATE KEY UPDATE Username = VALUES(Username);

-- Gestionnaire de documents
-- Mot de passe réel: manager123
INSERT INTO Users (Username, Email, PasswordHash, Role, CreatedAt, IsActive) 
VALUES (
    'manager', 
    'manager@docapi.com', 
    '$2a$11$UJjhvS6.M0sraEQT7ZOP8u1x1XMJ5ppsxCNeyuEIrEq.IKU7PbBY6', 
    'User', 
    NOW(), 
    TRUE
) ON DUPLICATE KEY UPDATE Username = VALUES(Username);

-- =====================================================
-- Données de test pour TypeDocument (après Users)
-- =====================================================
INSERT INTO TypeDocument (Name, Description, CreatedAt, CreatedByUserId) VALUES
('PDF', 'Documents au format PDF', NOW(), 1),
('Word', 'Documents Microsoft Word', NOW(), 1),
('Excel', 'Feuilles de calcul Excel', NOW(), 1),
('PowerPoint', 'Présentations PowerPoint', NOW(), 1),
('Image', 'Fichiers images (JPG, PNG, etc.)', NOW(), 1),
('Contrat', 'Documents contractuels', NOW(), 1),
('Facture', 'Documents de facturation', NOW(), 1),
('Rapport', 'Rapports et analyses', NOW(), 1)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

-- =====================================================
-- Données de test pour Document (après Users et TypeDocument)
-- =====================================================
INSERT INTO Document (Title, FilePath, TypeDocumentId, CreatedAt, CreatedByUserId) VALUES
('Manuel utilisateur', '/documents/manuel_utilisateur.pdf', 1, NOW(), 1),
('Contrat de service', '/documents/contrat_service.pdf', 6, NOW(), 1),
('Rapport mensuel', '/documents/rapport_janvier_2024.pdf', 8, NOW(), 2),
('Présentation projet', '/documents/presentation_projet.pptx', 4, NOW(), 2),
('Facture 2024-001', '/documents/facture_001.pdf', 7, NOW(), 3)
ON DUPLICATE KEY UPDATE Title = VALUES(Title);

-- =====================================================
-- Vérification des données insérées
-- =====================================================
SELECT 'TypeDocument' as TableName, COUNT(*) as RecordCount FROM TypeDocument
UNION ALL
SELECT 'Document' as TableName, COUNT(*) as RecordCount FROM Document
UNION ALL
SELECT 'Users' as TableName, COUNT(*) as RecordCount FROM Users;