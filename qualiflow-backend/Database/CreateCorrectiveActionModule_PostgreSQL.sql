-- CORRECTIVE ACTION MODULE SETUP FOR POSTGRESQL
-- Compatible with current project (Dapper + Npgsql)

CREATE TABLE IF NOT EXISTS CorrectiveActions (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    NonConformityId INTEGER NOT NULL REFERENCES NonConformities(Id) ON DELETE CASCADE,
    Type VARCHAR(20) NOT NULL DEFAULT 'CORRECTIVE' CHECK (Type IN ('CURATIVE', 'CORRECTIVE', 'RISQUE')),
    Title VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    ResponsibleUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    DueDate TIMESTAMP NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'PLANIFIEE' CHECK (Status IN ('PLANIFIEE', 'EN_COURS', 'REALISEE', 'VERIFIEE')),
    CompletionDate TIMESTAMP NULL,
    EffectivenessVerified BOOLEAN NULL,
    EffectivenessComment TEXT NULL,
    ProofRecordId INTEGER NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL
);

ALTER TABLE CorrectiveActions ADD COLUMN IF NOT EXISTS Type VARCHAR(20) NOT NULL DEFAULT 'CORRECTIVE';
ALTER TABLE CorrectiveActions ADD COLUMN IF NOT EXISTS Status VARCHAR(20) NOT NULL DEFAULT 'PLANIFIEE';
ALTER TABLE CorrectiveActions ADD COLUMN IF NOT EXISTS EffectivenessVerified BOOLEAN NULL;
ALTER TABLE CorrectiveActions ADD COLUMN IF NOT EXISTS EffectivenessComment TEXT NULL;
ALTER TABLE CorrectiveActions ADD COLUMN IF NOT EXISTS ProofRecordId INTEGER NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'correctiveactions_status_check'
    ) THEN
        ALTER TABLE CorrectiveActions DROP CONSTRAINT correctiveactions_status_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'correctiveactions_type_check'
    ) THEN
        ALTER TABLE CorrectiveActions DROP CONSTRAINT correctiveactions_type_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_correctiveactions_status'
    ) THEN
        ALTER TABLE CorrectiveActions DROP CONSTRAINT ck_correctiveactions_status;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_correctiveactions_type'
    ) THEN
        ALTER TABLE CorrectiveActions DROP CONSTRAINT ck_correctiveactions_type;
    END IF;
END $$;

UPDATE CorrectiveActions
SET Type = 'CORRECTIVE'
WHERE Type IS NULL OR TRIM(Type) = '';

UPDATE CorrectiveActions
SET Status = CASE
    WHEN Status = 'A_FAIRE' THEN 'PLANIFIEE'
    WHEN Status = 'TERMINEE' THEN 'REALISEE'
    WHEN Status = 'EN_RETARD' THEN 'EN_COURS'
    ELSE Status
END;

UPDATE CorrectiveActions
SET Status = 'PLANIFIEE'
WHERE Status NOT IN ('PLANIFIEE', 'EN_COURS', 'REALISEE', 'VERIFIEE');

DO $$
BEGIN

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_correctiveactions_status'
    ) THEN
        ALTER TABLE CorrectiveActions
        ADD CONSTRAINT ck_correctiveactions_status
        CHECK (Status IN ('PLANIFIEE', 'EN_COURS', 'REALISEE', 'VERIFIEE'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_correctiveactions_type'
    ) THEN
        ALTER TABLE CorrectiveActions
        ADD CONSTRAINT ck_correctiveactions_type
        CHECK (Type IN ('CURATIVE', 'CORRECTIVE', 'RISQUE'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_correctiveactions_proofrecord'
    ) THEN
        ALTER TABLE CorrectiveActions
        ADD CONSTRAINT fk_correctiveactions_proofrecord
        FOREIGN KEY (ProofRecordId) REFERENCES Documents(Id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS CorrectiveActionHistories (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    CorrectiveActionId INTEGER NOT NULL REFERENCES CorrectiveActions(Id) ON DELETE CASCADE,
    OldStatus VARCHAR(20) NULL,
    NewStatus VARCHAR(20) NOT NULL,
    Comment TEXT NULL,
    ChangedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    ChangedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_correctiveactions_org ON CorrectiveActions(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_nc ON CorrectiveActions(NonConformityId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_responsible ON CorrectiveActions(ResponsibleUserId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_status ON CorrectiveActions(Status);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_due ON CorrectiveActions(DueDate);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_type ON CorrectiveActions(Type);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_proof ON CorrectiveActions(ProofRecordId);

CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_action ON CorrectiveActionHistories(CorrectiveActionId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_org ON CorrectiveActionHistories(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_changedat ON CorrectiveActionHistories(ChangedAt);

-- Demo seed data
DO $$
DECLARE
    org_id INTEGER;
    qualite_user_id INTEGER;
    chef_user_id INTEGER;
    support_user_id INTEGER;
    nc_1_id INTEGER;
    nc_2_id INTEGER;
    nc_3_id INTEGER;
BEGIN
    SELECT Id INTO org_id FROM Organizations WHERE Code = 'DEMO' LIMIT 1;

    IF org_id IS NULL THEN
        RAISE NOTICE 'Demo organization (DEMO) not found, corrective action seed skipped.';
        RETURN;
    END IF;

    SELECT Id INTO qualite_user_id FROM Users WHERE Email = 'qualite@demo.local' LIMIT 1;
    SELECT Id INTO chef_user_id FROM Users WHERE Email = 'chef@demo.local' LIMIT 1;
    SELECT Id INTO support_user_id FROM Users WHERE Email = 'user@demo.local' LIMIT 1;

    SELECT Id INTO nc_1_id FROM NonConformities WHERE OrganizationId = org_id AND Code = 'NC-001' LIMIT 1;
    SELECT Id INTO nc_2_id FROM NonConformities WHERE OrganizationId = org_id AND Code = 'NC-002' LIMIT 1;
    SELECT Id INTO nc_3_id FROM NonConformities WHERE OrganizationId = org_id ORDER BY Id ASC LIMIT 1;

    IF nc_1_id IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1 FROM CorrectiveActions
            WHERE OrganizationId = org_id AND NonConformityId = nc_1_id AND Title = 'AC-001 - Corriger l''absence de validation documentaire'
        ) THEN
            INSERT INTO CorrectiveActions
                (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
            VALUES
                (org_id, nc_1_id, 'CORRECTIVE', 'AC-001 - Corriger l''absence de validation documentaire',
                 'Mettre en place un point de controle bloquant avant validation finale.',
                 COALESCE(qualite_user_id, chef_user_id, 1), NOW() + INTERVAL '5 day', 'EN_COURS', NULL, NOW());
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM CorrectiveActions
            WHERE OrganizationId = org_id AND NonConformityId = nc_1_id AND Title = 'AC-002 - Reviser la procedure d''inscription'
        ) THEN
            INSERT INTO CorrectiveActions
                (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
            VALUES
                (org_id, nc_1_id, 'CURATIVE', 'AC-002 - Reviser la procedure d''inscription',
                 'Actualiser la procedure et informer toutes les equipes concernees.',
                 COALESCE(chef_user_id, qualite_user_id, 1), NOW() + INTERVAL '12 day', 'PLANIFIEE', NULL, NOW());
        END IF;
    END IF;

    IF nc_2_id IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1 FROM CorrectiveActions
            WHERE OrganizationId = org_id AND NonConformityId = nc_2_id AND Title = 'AC-003 - Former le personnel sur le processus qualite'
        ) THEN
            INSERT INTO CorrectiveActions
                (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
            VALUES
                (org_id, nc_2_id, 'CORRECTIVE', 'AC-003 - Former le personnel sur le processus qualite',
                 'Lancer une session de formation et faire signer les feuilles de presence.',
                 COALESCE(qualite_user_id, support_user_id, 1), NOW() - INTERVAL '2 day', 'EN_COURS', NULL, NOW());
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM CorrectiveActions
            WHERE OrganizationId = org_id AND NonConformityId = nc_2_id AND Title = 'AC-004 - Mettre en place une verification mensuelle'
        ) THEN
            INSERT INTO CorrectiveActions
                (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
            VALUES
                (org_id, nc_2_id, 'RISQUE', 'AC-004 - Mettre en place une verification mensuelle',
                 'Instaurer un controle mensuel avec rapport de suivi des ecarts.',
                 COALESCE(support_user_id, qualite_user_id, 1), NOW() - INTERVAL '10 day', 'REALISEE', NOW() - INTERVAL '3 day', NOW());
        END IF;
    ELSIF nc_3_id IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1 FROM CorrectiveActions
            WHERE OrganizationId = org_id AND NonConformityId = nc_3_id AND Title = 'AC-004 - Mettre en place une verification mensuelle'
        ) THEN
            INSERT INTO CorrectiveActions
                (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
            VALUES
                (org_id, nc_3_id, 'RISQUE', 'AC-004 - Mettre en place une verification mensuelle',
                 'Instaurer un controle mensuel avec rapport de suivi des ecarts.',
                 COALESCE(support_user_id, qualite_user_id, 1), NOW() - INTERVAL '10 day', 'REALISEE', NOW() - INTERVAL '3 day', NOW());
        END IF;
    END IF;
END $$;
