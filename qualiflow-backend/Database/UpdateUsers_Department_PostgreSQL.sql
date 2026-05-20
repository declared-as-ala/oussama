-- ============================================================
-- USERS MIGRATION FOR DEPARTMENT MODULE
-- ============================================================

ALTER TABLE Users ADD COLUMN IF NOT EXISTS DepartmentId INTEGER NULL;

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

CREATE INDEX IF NOT EXISTS idx_users_department ON Users(DepartmentId);
