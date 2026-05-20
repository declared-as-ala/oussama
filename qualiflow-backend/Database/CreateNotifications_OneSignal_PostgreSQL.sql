-- OneSignal-ready notification history table (greenfield schema)
-- Uses UUID identifiers and channel/provider metadata.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" VARCHAR(255) NOT NULL,
    "Message" TEXT NOT NULL,
    "Type" VARCHAR(80) NOT NULL, -- Document, Workflow, Alert, ...
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "DocumentId" INTEGER NULL REFERENCES "Documents"("Id") ON DELETE SET NULL,
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Channel" VARCHAR(20) NOT NULL CHECK ("Channel" IN ('PUSH', 'INAPP', 'EMAIL')),
    "ExternalProviderId" VARCHAR(255) NULL
);

CREATE INDEX IF NOT EXISTS idx_notifications_user_created
    ON "Notifications"("UserId", "CreatedAt" DESC);

CREATE INDEX IF NOT EXISTS idx_notifications_read
    ON "Notifications"("IsRead");

CREATE INDEX IF NOT EXISTS idx_notifications_type
    ON "Notifications"("Type");

CREATE INDEX IF NOT EXISTS idx_notifications_document
    ON "Notifications"("DocumentId");

CREATE INDEX IF NOT EXISTS idx_notifications_channel
    ON "Notifications"("Channel");
