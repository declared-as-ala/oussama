-- =====================================================
-- NOTIFICATION PUSH EXTENSIONS (POSTGRESQL)
-- =====================================================

ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "SenderId" INTEGER NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "IsPushSent" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "EntityType" VARCHAR(100) NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "EntityId" INTEGER NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "RedirectUrl" VARCHAR(500) NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "ExpiresAt" TIMESTAMP NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_notifications_sender'
    ) THEN
        ALTER TABLE "Notifications"
        ADD CONSTRAINT "fk_notifications_sender"
        FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_notifications_push_sent ON "Notifications"("IsPushSent");

CREATE TABLE IF NOT EXISTS "UserDevices" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "DeviceToken" TEXT NOT NULL,
    "Platform" VARCHAR(20) NOT NULL,
    "DeviceName" VARCHAR(255) NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "LastSeenAt" TIMESTAMP NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_userdevices_user_token_unique ON "UserDevices"("UserId", "DeviceToken");
CREATE INDEX IF NOT EXISTS idx_userdevices_user ON "UserDevices"("UserId");
CREATE INDEX IF NOT EXISTS idx_userdevices_active ON "UserDevices"("IsActive");
