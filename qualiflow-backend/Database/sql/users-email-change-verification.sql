-- QualiFlow - Profile email change verification support

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS PendingEmail VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS EmailChangeVerificationToken VARCHAR(20) NULL,
    ADD COLUMN IF NOT EXISTS EmailChangeVerificationExpiresAt TIMESTAMP NULL;

CREATE INDEX IF NOT EXISTS idx_users_pending_email
    ON Users (PendingEmail)
    WHERE PendingEmail IS NOT NULL;
