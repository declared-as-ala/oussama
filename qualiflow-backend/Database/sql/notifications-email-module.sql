-- QualiFlow - Notification email dispatch module (PostgreSQL)
-- Safe migration script for existing Notifications table

ALTER TABLE Notifications
    ADD COLUMN IF NOT EXISTS TargetRole VARCHAR(80) NULL,
    ADD COLUMN IF NOT EXISTS EmailSent BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS EmailSentAt TIMESTAMP NULL,
    ADD COLUMN IF NOT EXISTS EmailError TEXT NULL,
    ADD COLUMN IF NOT EXISTS EmailAttemptCount INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS EmailNextAttemptAt TIMESTAMP NULL;

-- Optional hardening: if old rows have NULL EmailSent values
UPDATE Notifications
SET EmailSent = FALSE
WHERE EmailSent IS NULL;

-- Helpful indexes for dispatcher polling and tenant-safe filtering
CREATE INDEX IF NOT EXISTS idx_notifications_email_sent
    ON Notifications (EmailSent);

CREATE INDEX IF NOT EXISTS idx_notifications_org_targetrole_active
    ON Notifications (OrganizationId, TargetRole, EmailSent);

CREATE INDEX IF NOT EXISTS idx_notifications_email_pending_created
    ON Notifications (EmailSent, CreatedAt);

CREATE INDEX IF NOT EXISTS idx_notifications_email_retry
    ON Notifications (EmailSent, EmailNextAttemptAt, CreatedAt);

-- Optional index for faster recipients lookup when users table stores role directly
CREATE INDEX IF NOT EXISTS idx_users_org_role_active_email
    ON Users (OrganizationId, Role, IsActive, Email);
