CREATE TABLE IF NOT EXISTS CorrectiveActionAttachments (
    Id SERIAL PRIMARY KEY,
    CorrectiveActionId INTEGER NOT NULL REFERENCES CorrectiveActions(Id) ON DELETE CASCADE,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    FileName VARCHAR(260) NOT NULL,
    OriginalFileName VARCHAR(260) NOT NULL,
    FileExtension VARCHAR(20) NULL,
    MimeType VARCHAR(150) NULL,
    FileSize BIGINT NULL,
    FileContent BYTEA NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_correctiveactionattachments_action
    ON CorrectiveActionAttachments(CorrectiveActionId);

CREATE INDEX IF NOT EXISTS idx_correctiveactionattachments_org
    ON CorrectiveActionAttachments(OrganizationId);
