-- ============================================================
-- DOCUMENTS MIGRATION FOR DEPARTMENT MODULE
-- ============================================================

ALTER TABLE Documents ADD COLUMN IF NOT EXISTS DepartmentId INTEGER NULL;

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

CREATE INDEX IF NOT EXISTS idx_documents_department ON Documents(DepartmentId);
