-- 🚀 MIGRATION: SCOPED EMAIL UNIQUENESS
-- Allows the same email to be used for multiple accounts in different organizations.

DO $$
BEGIN
    -- 1. Remove existing global uniqueness on Email
    -- Check for various potential constraint/index names
    ALTER TABLE "Users" DROP CONSTRAINT IF EXISTS "Users_Email_key";
    ALTER TABLE "Users" DROP CONSTRAINT IF EXISTS "Users_Username_key"; -- Also drop username uniqueness if it's the same as email
    DROP INDEX IF EXISTS idx_email;
    DROP INDEX IF EXISTS idx_user_email;
    DROP INDEX IF EXISTS idx_username;

    -- 2. Create composite unique index (Email, OrganizationId)
    -- Allows: 
    -- ('test@email.com', 1) 
    -- ('test@email.com', 2)
    -- This allows duplicate emails ACROSS organizations, but NOT within the same one.
    CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_org_unique ON "Users" ("Email", "OrganizationId");

    -- 3. Ensure global uniqueness for Super Admins (OrganizationId IS NULL)
    CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_superadmin_unique ON "Users" ("Email") WHERE "OrganizationId" IS NULL;

    RAISE NOTICE 'Scoped email uniqueness migration completed successfully.';
END $$;
