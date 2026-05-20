-- =====================================================
-- DOCUMENT NOTIFICATION WORKFLOW - PostgreSQL
-- Tables: document_notifications, notification_rules, document_expiration_policies
-- =====================================================

CREATE TABLE IF NOT EXISTS document_notifications (
    id SERIAL PRIMARY KEY,
    organization_id INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    document_id INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
    document_version_id INTEGER NULL REFERENCES DocumentVersions(Id) ON DELETE SET NULL,
    event_type VARCHAR(60) NOT NULL,
    recipient_user_id INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    recipient_role VARCHAR(30) NOT NULL,
    channel VARCHAR(20) NOT NULL DEFAULT 'EMAIL',
    subject VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    delivery_status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    external_message_id VARCHAR(255) NULL,
    payload_json TEXT NULL,
    sent_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS notification_rules (
    id SERIAL PRIMARY KEY,
    organization_id INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    event_type VARCHAR(60) NOT NULL,
    role_type VARCHAR(30) NOT NULL,
    restrict_to_document_department BOOLEAN NOT NULL DEFAULT FALSE,
    email_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    in_app_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_notification_rules_unique UNIQUE (organization_id, event_type, role_type)
);

CREATE TABLE IF NOT EXISTS document_expiration_policies (
    id SERIAL PRIMARY KEY,
    organization_id INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    alert_days_30 INTEGER NOT NULL DEFAULT 30,
    alert_days_7 INTEGER NOT NULL DEFAULT 7,
    alert_days_1 INTEGER NOT NULL DEFAULT 1,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NULL,
    CONSTRAINT uq_document_expiration_policies_org UNIQUE (organization_id)
);

CREATE INDEX IF NOT EXISTS idx_document_notifications_org ON document_notifications(organization_id);
CREATE INDEX IF NOT EXISTS idx_document_notifications_doc ON document_notifications(document_id);
CREATE INDEX IF NOT EXISTS idx_document_notifications_event ON document_notifications(event_type);
CREATE INDEX IF NOT EXISTS idx_document_notifications_created ON document_notifications(created_at);
CREATE INDEX IF NOT EXISTS idx_notification_rules_org_event ON notification_rules(organization_id, event_type);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'documentversions_status_check'
    ) THEN
        ALTER TABLE DocumentVersions
        DROP CONSTRAINT documentversions_status_check;
    END IF;

    ALTER TABLE DocumentVersions
    ADD CONSTRAINT documentversions_status_check
    CHECK (Status IN ('BROUILLON', 'EN_REVISION', 'APPROUVE', 'PUBLIE', 'REJETE', 'PERIME', 'ARCHIVE'));
EXCEPTION
    WHEN duplicate_object THEN NULL;
END $$;
