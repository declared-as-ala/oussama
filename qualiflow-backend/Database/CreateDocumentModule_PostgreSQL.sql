-- ============================================================
-- GED MODULE - PostgreSQL
-- Tables: Documents, DocumentVersions, DocumentVersions
-- ============================================================

CREATE TABLE IF NOT EXISTS Documents (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    ProcessId INTEGER NULL REFERENCES Processes(Id) ON DELETE SET NULL,
    ProcedureId INTEGER NULL REFERENCES Procedures(Id) ON DELETE SET NULL,
    Code VARCHAR(50) NOT NULL,
    Title VARCHAR(255) NOT NULL,
    Type VARCHAR(30) NOT NULL CHECK (Type IN ('MANUEL', 'PROCEDURE', 'ENREGISTREMENT', 'FORMULAIRE', 'INSTRUCTION', 'POLITIQUE', 'AUTRE')),
    Description TEXT NULL,
    Category VARCHAR(120) NULL,
    Keywords TEXT NULL,
    Signature TEXT NULL,
    OwnerUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    DepartmentId INTEGER NULL REFERENCES Departments(Id) ON DELETE SET NULL,
    CurrentVersionId INTEGER NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    DeletedAt TIMESTAMP NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL
);
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS DeletedAt TIMESTAMP NULL;
CREATE UNIQUE INDEX uq_documents_org_code_active ON Documents (OrganizationId, Code) WHERE (DeletedAt IS NULL);

CREATE TABLE IF NOT EXISTS DocumentVersions (
    Id SERIAL PRIMARY KEY,
    DocumentId INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    VersionNumber VARCHAR(30) NOT NULL,
    Status VARCHAR(20) NOT NULL CHECK (Status IN ('BROUILLON', 'EN_REVISION', 'APPROUVE', 'PUBLIE', 'REJETE', 'PERIME', 'ARCHIVE')),
    FileName VARCHAR(260) NULL,
    OriginalFileName VARCHAR(260) NULL,
    FilePath TEXT NULL,
    FileExtension VARCHAR(20) NULL,
    MimeType VARCHAR(150) NULL,
    FileSize BIGINT NULL,
    RevisionComment TEXT NULL,
    EstablishedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    EstablishedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    VerifiedByUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    VerifiedAt TIMESTAMP NULL,
    ValidatedByUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    ValidatedAt TIMESTAMP NULL,
    EffectiveDate TIMESTAMP NULL,
    ExpiryDate TIMESTAMP NULL,
    IsCurrent BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_documentversions_document_version UNIQUE (DocumentId, VersionNumber)
);

CREATE TABLE IF NOT EXISTS DocumentVersions (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    DocumentId INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
    DocumentVersionId INTEGER NULL REFERENCES DocumentVersions(Id) ON DELETE SET NULL,
    Action VARCHAR(80) NOT NULL,
    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    Details TEXT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_documents_currentversion'
    ) THEN
        ALTER TABLE Documents
        ADD CONSTRAINT fk_documents_currentversion
        FOREIGN KEY (CurrentVersionId) REFERENCES DocumentVersions(Id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_documents_org ON Documents(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_documents_code ON Documents(Code);
CREATE INDEX IF NOT EXISTS idx_documents_type ON Documents(Type);
CREATE INDEX IF NOT EXISTS idx_documents_owner ON Documents(OwnerUserId);
CREATE INDEX IF NOT EXISTS idx_documents_department ON Documents(DepartmentId);
CREATE INDEX IF NOT EXISTS idx_documents_process ON Documents(ProcessId);
CREATE INDEX IF NOT EXISTS idx_documents_procedure ON Documents(ProcedureId);
CREATE INDEX IF NOT EXISTS idx_documents_currentversion ON Documents(CurrentVersionId);
CREATE INDEX IF NOT EXISTS idx_documents_active ON Documents(IsActive);
CREATE INDEX IF NOT EXISTS idx_documents_deletedat ON Documents(DeletedAt);
CREATE INDEX IF NOT EXISTS idx_documents_updatedat ON Documents(UpdatedAt);

CREATE INDEX IF NOT EXISTS idx_documentversions_org ON DocumentVersions(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_documentversions_document ON DocumentVersions(DocumentId);
CREATE INDEX IF NOT EXISTS idx_documentversions_status ON DocumentVersions(Status);
CREATE INDEX IF NOT EXISTS idx_documentversions_current ON DocumentVersions(DocumentId, IsCurrent);
CREATE INDEX IF NOT EXISTS idx_documentversions_established ON DocumentVersions(EstablishedAt);


-- ============================================================
-- DEMO DATA (adapte selon ton environnement)
-- ============================================================
-- Exemples de codes attendus:
-- DOC-MAN-001, DOC-PROC-001, DOC-ENR-001, DOC-FORM-001
