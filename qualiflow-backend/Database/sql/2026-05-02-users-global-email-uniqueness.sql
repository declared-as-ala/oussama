-- Restore global email uniqueness and disable multi-organization login.
-- If duplicates already exist, resolve them manually before creating the unique index.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "Users"
        GROUP BY "Email"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Duplicate user emails exist. Resolve duplicated emails before applying global uniqueness.';
    END IF;
END $$;

DROP INDEX IF EXISTS idx_users_email_org_unique;
DROP INDEX IF EXISTS idx_users_email_superadmin_unique;

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_unique ON "Users" ("Email");
