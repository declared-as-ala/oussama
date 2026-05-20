-- ============================================================
-- DEPARTMENT MODULE - PostgreSQL
-- Tables: Departments
-- ============================================================

CREATE TABLE IF NOT EXISTS Departments (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    Name VARCHAR(255) NOT NULL,
    Code VARCHAR(50) NOT NULL,
    Description TEXT NULL,
    ManagerUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK (Status IN ('ACTIF', 'INACTIF')),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_departments_org_code UNIQUE (OrganizationId, Code)
);

ALTER TABLE Users ADD COLUMN IF NOT EXISTS DepartmentId INTEGER NULL;
ALTER TABLE Documents ADD COLUMN IF NOT EXISTS DepartmentId INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_users_department'
    ) THEN
        ALTER TABLE Users
        ADD CONSTRAINT fk_users_department
        FOREIGN KEY (DepartmentId) REFERENCES Departments(Id) ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_documents_department'
    ) THEN
        ALTER TABLE Documents
        ADD CONSTRAINT fk_documents_department
        FOREIGN KEY (DepartmentId) REFERENCES Departments(Id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_departments_org ON Departments(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_departments_code ON Departments(Code);
CREATE INDEX IF NOT EXISTS idx_departments_name ON Departments(Name);
CREATE INDEX IF NOT EXISTS idx_departments_status ON Departments(Status);
CREATE INDEX IF NOT EXISTS idx_departments_manager ON Departments(ManagerUserId);

CREATE INDEX IF NOT EXISTS idx_users_department ON Users(DepartmentId);
CREATE INDEX IF NOT EXISTS idx_documents_department ON Documents(DepartmentId);

-- ============================================================
-- DEMO DATA
-- ============================================================
-- Exemple de départements pour une organisation de démonstration:
-- Service Qualite
-- Service Informatique
-- Ressources Humaines
-- Service Scolarite
