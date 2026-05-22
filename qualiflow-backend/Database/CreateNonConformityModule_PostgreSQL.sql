-- NON-CONFORMITY MODULE SETUP FOR POSTGRESQL
-- Compatible with current project (Dapper + Npgsql)

CREATE TABLE IF NOT EXISTS NonConformities (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    Code VARCHAR(50) NULL,
    Title VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    Type VARCHAR(20) NOT NULL CHECK (Type IN ('INTERNE', 'EXTERNE')),
    Severity VARCHAR(20) NOT NULL CHECK (Severity IN ('MINEURE', 'MAJEURE', 'CRITIQUE')),
    ProcessId INTEGER NULL REFERENCES Processes(Id) ON DELETE SET NULL,
    ProcedureId INTEGER NULL REFERENCES Procedures(Id) ON DELETE SET NULL,
    DetectedDate TIMESTAMP NOT NULL,
    ResponsibleUserId INTEGER NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    Status VARCHAR(30) NOT NULL DEFAULT 'EN_ATTENTE_VALIDATION' CHECK (Status IN ('EN_ATTENTE_VALIDATION', 'OUVERTE', 'EN_COURS', 'CLOTUREE')),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_nonconformities_org_code UNIQUE (OrganizationId, Code)
);

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
    ProofRecordId INTEGER NULL REFERENCES Documents(Id) ON DELETE SET NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL
);

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

CREATE INDEX IF NOT EXISTS idx_nonconformities_org ON NonConformities(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_nonconformities_code ON NonConformities(Code);
CREATE INDEX IF NOT EXISTS idx_nonconformities_title ON NonConformities(Title);
CREATE INDEX IF NOT EXISTS idx_nonconformities_type ON NonConformities(Type);
CREATE INDEX IF NOT EXISTS idx_nonconformities_severity ON NonConformities(Severity);
CREATE INDEX IF NOT EXISTS idx_nonconformities_process ON NonConformities(ProcessId);
CREATE INDEX IF NOT EXISTS idx_nonconformities_procedure ON NonConformities(ProcedureId);
CREATE INDEX IF NOT EXISTS idx_nonconformities_responsible ON NonConformities(ResponsibleUserId);
CREATE INDEX IF NOT EXISTS idx_nonconformities_status ON NonConformities(Status);
CREATE INDEX IF NOT EXISTS idx_nonconformities_detected ON NonConformities(DetectedDate);

CREATE INDEX IF NOT EXISTS idx_correctiveactions_org ON CorrectiveActions(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_nc ON CorrectiveActions(NonConformityId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_responsible ON CorrectiveActions(ResponsibleUserId);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_status ON CorrectiveActions(Status);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_due ON CorrectiveActions(DueDate);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_type ON CorrectiveActions(Type);
CREATE INDEX IF NOT EXISTS idx_correctiveactions_proof ON CorrectiveActions(ProofRecordId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionattachments_action ON CorrectiveActionAttachments(CorrectiveActionId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionattachments_org ON CorrectiveActionAttachments(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_action ON CorrectiveActionHistories(CorrectiveActionId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_org ON CorrectiveActionHistories(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_correctiveactionhistories_changedat ON CorrectiveActionHistories(ChangedAt);

-- Demo seed data
DO $$
DECLARE
    org_id INTEGER;
    process_realisation_id INTEGER;
    process_support_id INTEGER;
    procedure_2_id INTEGER;
    procedure_3_id INTEGER;
    qualite_user_id INTEGER;
    chef_user_id INTEGER;
    support_user_id INTEGER;
    nc_1_id INTEGER;
    nc_2_id INTEGER;
BEGIN
    SELECT Id INTO org_id FROM Organizations WHERE Code = 'DEMO' LIMIT 1;

    IF org_id IS NULL THEN
        RAISE NOTICE 'Demo organization (DEMO) not found, non-conformity seed skipped.';
        RETURN;
    END IF;

    SELECT Id INTO process_realisation_id FROM Processes WHERE OrganizationId = org_id AND Code = 'REA-001' LIMIT 1;
    SELECT Id INTO process_support_id FROM Processes WHERE OrganizationId = org_id AND Code = 'SUP-001' LIMIT 1;
    SELECT Id INTO procedure_2_id FROM Procedures WHERE OrganizationId = org_id AND Code = 'PROC-002' LIMIT 1;
    SELECT Id INTO procedure_3_id FROM Procedures WHERE OrganizationId = org_id AND Code = 'PROC-003' LIMIT 1;

    SELECT Id INTO qualite_user_id FROM Users WHERE Email = 'qualite@demo.local' LIMIT 1;
    SELECT Id INTO chef_user_id FROM Users WHERE Email = 'chef@demo.local' LIMIT 1;
    SELECT Id INTO support_user_id FROM Users WHERE Email = 'user@demo.local' LIMIT 1;

    INSERT INTO NonConformities (
        OrganizationId, Code, Title, Description, Type, Severity, ProcessId, ProcedureId, DetectedDate, ResponsibleUserId, Status, CreatedAt
    ) VALUES (
        org_id,
        'NC-001',
        'Dossier inscription incomplet',
        'Des dossiers ont ete valides sans l''ensemble des pieces justificatives.',
        'INTERNE',
        'MAJEURE',
        process_realisation_id,
        procedure_2_id,
        NOW() - INTERVAL '6 days',
        COALESCE(chef_user_id, qualite_user_id),
        'EN_COURS',
        NOW()
    ) ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO NonConformities (
        OrganizationId, Code, Title, Description, Type, Severity, ProcessId, ProcedureId, DetectedDate, ResponsibleUserId, Status, CreatedAt
    ) VALUES (
        org_id,
        'NC-002',
        'Interruption critique du service informatique',
        'Indisponibilite de la plateforme qualite superieure a 4 heures.',
        'EXTERNE',
        'CRITIQUE',
        process_support_id,
        procedure_3_id,
        NOW() - INTERVAL '10 days',
        COALESCE(support_user_id, qualite_user_id),
        'OUVERTE',
        NOW()
    ) ON CONFLICT (OrganizationId, Code) DO NOTHING;

    SELECT Id INTO nc_1_id FROM NonConformities WHERE OrganizationId = org_id AND Code = 'NC-001' LIMIT 1;
    SELECT Id INTO nc_2_id FROM NonConformities WHERE OrganizationId = org_id AND Code = 'NC-002' LIMIT 1;

    IF nc_1_id IS NOT NULL THEN
        INSERT INTO CorrectiveActions (
            OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt
        ) VALUES
            (org_id, nc_1_id, 'CORRECTIVE', 'Mettre en place une check-list obligatoire', 'Ajouter un controle bloquant avant validation du dossier.', COALESCE(chef_user_id, qualite_user_id), NOW() + INTERVAL '7 days', 'EN_COURS', NULL, NOW()),
            (org_id, nc_1_id, 'CURATIVE', 'Former l''equipe scolarite', 'Session de sensibilisation sur les exigences documentaires.', COALESCE(qualite_user_id, chef_user_id), NOW() + INTERVAL '3 days', 'PLANIFIEE', NULL, NOW())
        ON CONFLICT DO NOTHING;
    END IF;

    IF nc_2_id IS NOT NULL THEN
        INSERT INTO CorrectiveActions (
            OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt
        ) VALUES
            (org_id, nc_2_id, 'RISQUE', 'Renforcer la supervision proactive', 'Activer des alertes temps reel et une astreinte technique.', COALESCE(support_user_id, qualite_user_id), NOW() - INTERVAL '1 day', 'EN_COURS', NULL, NOW())
        ON CONFLICT DO NOTHING;
    END IF;
END $$;
