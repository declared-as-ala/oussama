-- Notification module extension (2026-04-22)
-- 1) Add SourceModule to Notifications
-- 2) Add UserWebPushSubscriptions structure for browser push subscriptions

ALTER TABLE Notifications
ADD COLUMN IF NOT EXISTS SourceModule VARCHAR(100) NULL;

CREATE TABLE IF NOT EXISTS UserWebPushSubscriptions (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    OrganizationId INTEGER NULL REFERENCES Organizations(Id) ON DELETE SET NULL,
    Endpoint TEXT NOT NULL,
    P256dh VARCHAR(512) NOT NULL,
    Auth VARCHAR(512) NOT NULL,
    UserAgent TEXT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    LastUsedAt TIMESTAMP NULL,
    CONSTRAINT uq_webpush_user_endpoint UNIQUE (UserId, Endpoint)
);

CREATE INDEX IF NOT EXISTS idx_webpush_user_active ON UserWebPushSubscriptions(UserId, IsActive);
CREATE INDEX IF NOT EXISTS idx_webpush_org_active ON UserWebPushSubscriptions(OrganizationId, IsActive);
