-- =====================================================
-- NOTIFICATION MODULE SETUP FOR POSTGRESQL
-- =====================================================

CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id" SERIAL PRIMARY KEY,
    "PublicId" UUID NULL,
    "OrganizationId" INTEGER NULL REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "SenderId" INTEGER NULL REFERENCES "Users"("Id") ON DELETE SET NULL,
    "Type" VARCHAR(80) NOT NULL,
    "Category" VARCHAR(20) NOT NULL CHECK ("Category" IN ('INFO', 'SUCCESS', 'WARNING', 'ERROR')),
    "Title" VARCHAR(255) NOT NULL,
    "Message" TEXT NOT NULL,
    "Priority" VARCHAR(20) NOT NULL DEFAULT 'MEDIUM' CHECK ("Priority" IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "ReadAt" TIMESTAMP NULL,
    "IsPushSent" BOOLEAN NOT NULL DEFAULT FALSE,
    "Channel" VARCHAR(20) NOT NULL DEFAULT 'INAPP',
    "ExternalProviderId" VARCHAR(255) NULL,
    "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
    "DocumentId" INTEGER NULL,
    "EntityType" VARCHAR(100) NULL,
    "EntityId" INTEGER NULL,
    "RedirectUrl" VARCHAR(500) NULL,
    "ExpiresAt" TIMESTAMP NULL,
    "ReferenceType" VARCHAR(80) NULL,
    "ReferenceId" VARCHAR(80) NULL,
    "ActionUrl" VARCHAR(500) NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "SenderId" INTEGER NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "PublicId" UUID NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "IsPushSent" BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "Channel" VARCHAR(20) NOT NULL DEFAULT 'INAPP';
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "ExternalProviderId" VARCHAR(255) NULL;
ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "DocumentId" INTEGER NULL;
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

CREATE TABLE IF NOT EXISTS "AlertRules" (
    "Id" SERIAL PRIMARY KEY,
    "OrganizationId" INTEGER NULL REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    "Code" VARCHAR(60) NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Description" TEXT NULL,
    "EntityType" VARCHAR(80) NOT NULL,
    "TriggerType" VARCHAR(80) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "ThresholdValue" NUMERIC(12,2) NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS "NotificationPreferences" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "NotificationType" VARCHAR(80) NOT NULL,
    "InAppEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "EmailEnabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP NULL,
    CONSTRAINT "uq_notificationpreferences_user_type" UNIQUE ("UserId", "NotificationType")
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_alertrules_org_code_unique ON "AlertRules"("OrganizationId", "Code");
WITH duplicate_notifications AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "UserId", "Type", COALESCE("ReferenceType", ''), COALESCE("ReferenceId", '')
            ORDER BY "CreatedAt" ASC, "Id" ASC
        ) AS "RowNumber"
    FROM "Notifications"
    WHERE "IsArchived" = FALSE
)
DELETE FROM "Notifications"
WHERE "Id" IN (
    SELECT "Id"
    FROM duplicate_notifications
    WHERE "RowNumber" > 1
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_notifications_active_dedupe_unique
    ON "Notifications"("UserId", "Type", (COALESCE("ReferenceType", '')), (COALESCE("ReferenceId", '')))
    WHERE "IsArchived" = FALSE;

CREATE INDEX IF NOT EXISTS idx_notifications_user ON "Notifications"("UserId");
CREATE INDEX IF NOT EXISTS idx_notifications_org ON "Notifications"("OrganizationId");
CREATE INDEX IF NOT EXISTS idx_notifications_read ON "Notifications"("IsRead");
CREATE INDEX IF NOT EXISTS idx_notifications_priority ON "Notifications"("Priority");
CREATE INDEX IF NOT EXISTS idx_notifications_type ON "Notifications"("Type");
CREATE INDEX IF NOT EXISTS idx_notifications_archived ON "Notifications"("IsArchived");
CREATE INDEX IF NOT EXISTS idx_notifications_created ON "Notifications"("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_notifications_push_sent ON "Notifications"("IsPushSent");
CREATE INDEX IF NOT EXISTS idx_notifications_channel ON "Notifications"("Channel");
CREATE INDEX IF NOT EXISTS idx_notifications_document_id ON "Notifications"("DocumentId");
CREATE UNIQUE INDEX IF NOT EXISTS idx_notifications_public_id_unique ON "Notifications"("PublicId") WHERE "PublicId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_notificationpreferences_user ON "NotificationPreferences"("UserId");
CREATE UNIQUE INDEX IF NOT EXISTS idx_userdevices_user_token_unique ON "UserDevices"("UserId", "DeviceToken");
CREATE INDEX IF NOT EXISTS idx_userdevices_user ON "UserDevices"("UserId");
CREATE INDEX IF NOT EXISTS idx_userdevices_active ON "UserDevices"("IsActive");

-- Demo alert rules for DEMO organization
INSERT INTO "AlertRules" ("OrganizationId", "Code", "Name", "Description", "EntityType", "TriggerType", "IsActive", "CreatedAt")
SELECT o."Id", 'NONCONFORMITY_CRITICAL', 'Alerte non-conformite critique',
       'Alerte envoyee quand une non-conformite critique est ouverte.',
       'NON_CONFORMITY', 'ON_CRITICAL_OPEN', TRUE, NOW()
FROM "Organizations" o
WHERE o."Code" = 'DEMO'
ON CONFLICT ("OrganizationId", "Code") DO NOTHING;

INSERT INTO "AlertRules" ("OrganizationId", "Code", "Name", "Description", "EntityType", "TriggerType", "IsActive", "CreatedAt")
SELECT o."Id", 'CORRECTIVE_ACTION_OVERDUE', 'Alerte action corrective en retard',
       'Alerte envoyee pour les actions correctives en retard.',
       'CORRECTIVE_ACTION', 'ON_OVERDUE', TRUE, NOW()
FROM "Organizations" o
WHERE o."Code" = 'DEMO'
ON CONFLICT ("OrganizationId", "Code") DO NOTHING;

INSERT INTO "AlertRules" ("OrganizationId", "Code", "Name", "Description", "EntityType", "TriggerType", "IsActive", "CreatedAt")
SELECT o."Id", 'DOCUMENT_EXPIRED', 'Alerte document perime',
       'Alerte envoyee pour les documents perimes.',
       'DOCUMENT', 'ON_EXPIRED', TRUE, NOW()
FROM "Organizations" o
WHERE o."Code" = 'DEMO'
ON CONFLICT ("OrganizationId", "Code") DO NOTHING;

-- Demo notifications for active users in DEMO organization
INSERT INTO "Notifications"
    ("OrganizationId", "UserId", "Type", "Category", "Title", "Message", "Priority", "IsRead", "ReadAt", "IsArchived", "ReferenceType", "ReferenceId", "ActionUrl", "CreatedAt")
SELECT
    o."Id",
    u."Id",
    'DOCUMENT_APPROVAL_REQUIRED',
    'INFO',
    'Validation requise: DOC-001',
    'Le document DOC-001 est en attente de validation.',
    'MEDIUM',
    FALSE,
    NULL,
    FALSE,
    'DOCUMENT',
    'DOC-001',
    '/documents',
    NOW() - INTERVAL '10 minutes'
FROM "Organizations" o
INNER JOIN "Users" u ON u."OrganizationId" = o."Id" AND u."IsActive" = TRUE
WHERE o."Code" = 'DEMO'
  AND u."Role" IN ('ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR')
  AND NOT EXISTS (
      SELECT 1
      FROM "Notifications" n
      WHERE n."OrganizationId" = o."Id"
        AND n."UserId" = u."Id"
        AND n."Type" = 'DOCUMENT_APPROVAL_REQUIRED'
        AND n."ReferenceId" = 'DOC-001'
  );

INSERT INTO "Notifications"
    ("OrganizationId", "UserId", "Type", "Category", "Title", "Message", "Priority", "IsRead", "ReadAt", "IsArchived", "ReferenceType", "ReferenceId", "ActionUrl", "CreatedAt")
SELECT
    o."Id",
    u."Id",
    'NONCONFORMITY_CRITICAL',
    'ERROR',
    'NC-002 critique ouverte',
    'La non-conformite NC-002 est critique et ouverte.',
    'CRITICAL',
    FALSE,
    NULL,
    FALSE,
    'NON_CONFORMITY',
    'NC-002',
    '/non-conformities',
    NOW() - INTERVAL '8 minutes'
FROM "Organizations" o
INNER JOIN "Users" u ON u."OrganizationId" = o."Id" AND u."IsActive" = TRUE
WHERE o."Code" = 'DEMO'
  AND u."Role" IN ('ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR')
  AND NOT EXISTS (
      SELECT 1
      FROM "Notifications" n
      WHERE n."OrganizationId" = o."Id"
        AND n."UserId" = u."Id"
        AND n."Type" = 'NONCONFORMITY_CRITICAL'
        AND n."ReferenceId" = 'NC-002'
  );

INSERT INTO "Notifications"
    ("OrganizationId", "UserId", "Type", "Category", "Title", "Message", "Priority", "IsRead", "ReadAt", "IsArchived", "ReferenceType", "ReferenceId", "ActionUrl", "CreatedAt")
SELECT
    o."Id",
    u."Id",
    'CORRECTIVE_ACTION_OVERDUE',
    'WARNING',
    'AC-004 en retard',
    'L''action corrective AC-004 est en retard.',
    'HIGH',
    FALSE,
    NULL,
    FALSE,
    'CORRECTIVE_ACTION',
    'AC-004',
    '/corrective-actions',
    NOW() - INTERVAL '6 minutes'
FROM "Organizations" o
INNER JOIN "Users" u ON u."OrganizationId" = o."Id" AND u."IsActive" = TRUE
WHERE o."Code" = 'DEMO'
  AND u."Role" IN ('ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR')
  AND NOT EXISTS (
      SELECT 1
      FROM "Notifications" n
      WHERE n."OrganizationId" = o."Id"
        AND n."UserId" = u."Id"
        AND n."Type" = 'CORRECTIVE_ACTION_OVERDUE'
        AND n."ReferenceId" = 'AC-004'
  );

INSERT INTO "Notifications"
    ("OrganizationId", "UserId", "Type", "Category", "Title", "Message", "Priority", "IsRead", "ReadAt", "IsArchived", "ReferenceType", "ReferenceId", "ActionUrl", "CreatedAt")
SELECT
    o."Id",
    u."Id",
    'INDICATOR_ALERT',
    'WARNING',
    'KPI-003 sous seuil',
    'L''indicateur KPI-003 est passe sous le seuil d''alerte.',
    'HIGH',
    FALSE,
    NULL,
    FALSE,
    'INDICATOR',
    'KPI-003',
    '/indicators',
    NOW() - INTERVAL '4 minutes'
FROM "Organizations" o
INNER JOIN "Users" u ON u."OrganizationId" = o."Id" AND u."IsActive" = TRUE
WHERE o."Code" = 'DEMO'
  AND u."Role" IN ('ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR')
  AND NOT EXISTS (
      SELECT 1
      FROM "Notifications" n
      WHERE n."OrganizationId" = o."Id"
        AND n."UserId" = u."Id"
        AND n."Type" = 'INDICATOR_ALERT'
        AND n."ReferenceId" = 'KPI-003'
  );

INSERT INTO "Notifications"
    ("OrganizationId", "UserId", "Type", "Category", "Title", "Message", "Priority", "IsRead", "ReadAt", "IsArchived", "ReferenceType", "ReferenceId", "ActionUrl", "CreatedAt")
SELECT
    o."Id",
    u."Id",
    'DOCUMENT_NEW_VERSION',
    'SUCCESS',
    'Nouvelle version publiee',
    'Une nouvelle version du document DOC-001 est publiee.',
    'MEDIUM',
    FALSE,
    NULL,
    FALSE,
    'DOCUMENT',
    'DOC-001',
    '/documents',
    NOW() - INTERVAL '2 minutes'
FROM "Organizations" o
INNER JOIN "Users" u ON u."OrganizationId" = o."Id" AND u."IsActive" = TRUE
WHERE o."Code" = 'DEMO'
  AND u."Role" IN ('ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR')
  AND NOT EXISTS (
      SELECT 1
      FROM "Notifications" n
      WHERE n."OrganizationId" = o."Id"
        AND n."UserId" = u."Id"
        AND n."Type" = 'DOCUMENT_NEW_VERSION'
        AND n."ReferenceId" = 'DOC-001'
  );
