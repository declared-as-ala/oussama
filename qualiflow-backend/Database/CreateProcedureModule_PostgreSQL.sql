-- PROCEDURE MODULE SETUP FOR POSTGRESQL
-- Compatible with current project (Dapper + Npgsql)

CREATE TABLE IF NOT EXISTS Procedures (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    ProcessId INTEGER NOT NULL REFERENCES Processes(Id) ON DELETE CASCADE,
    Code VARCHAR(50) NOT NULL,
    Title VARCHAR(255) NOT NULL,
    Objective TEXT NULL,
    Scope TEXT NULL,
    Description TEXT NULL,
    ResponsibleUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK (Status IN ('ACTIF', 'INACTIF')),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_procedures_org_code UNIQUE (OrganizationId, Code)
);

CREATE TABLE IF NOT EXISTS Instructions (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    ProcedureId INTEGER NOT NULL REFERENCES Procedures(Id) ON DELETE CASCADE,
    Code VARCHAR(50) NOT NULL,
    Title VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK (Status IN ('ACTIF', 'INACTIF')),
    OrderIndex INTEGER NOT NULL DEFAULT 1,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_instructions_procedure_code UNIQUE (ProcedureId, Code)
);

CREATE INDEX IF NOT EXISTS idx_procedures_org ON Procedures(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_procedures_process ON Procedures(ProcessId);
CREATE INDEX IF NOT EXISTS idx_procedures_code ON Procedures(Code);
CREATE INDEX IF NOT EXISTS idx_procedures_title ON Procedures(Title);
CREATE INDEX IF NOT EXISTS idx_procedures_responsible ON Procedures(ResponsibleUserId);
CREATE INDEX IF NOT EXISTS idx_procedures_status ON Procedures(Status);
CREATE INDEX IF NOT EXISTS idx_procedures_createdat ON Procedures(CreatedAt);

CREATE INDEX IF NOT EXISTS idx_instructions_org ON Instructions(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_instructions_procedure ON Instructions(ProcedureId);
CREATE INDEX IF NOT EXISTS idx_instructions_status ON Instructions(Status);
CREATE INDEX IF NOT EXISTS idx_instructions_order ON Instructions(ProcedureId, OrderIndex);

-- Demo data bound to organization DEMO when available
DO $$
DECLARE
    org_id INTEGER;
    process_pilotage_id INTEGER;
    process_realisation_id INTEGER;
    process_support_id INTEGER;
    qualite_user_id INTEGER;
    chef_user_id INTEGER;
    user_demo_id INTEGER;
    procedure_1_id INTEGER;
    procedure_2_id INTEGER;
    procedure_3_id INTEGER;
BEGIN
    SELECT Id INTO org_id FROM Organizations WHERE Code = 'DEMO' LIMIT 1;

    IF org_id IS NULL THEN
        RAISE NOTICE 'Demo organization (DEMO) not found, procedure seed skipped.';
        RETURN;
    END IF;

    SELECT Id INTO process_pilotage_id FROM Processes WHERE OrganizationId = org_id AND Code = 'PIL-001' LIMIT 1;
    SELECT Id INTO process_realisation_id FROM Processes WHERE OrganizationId = org_id AND Code = 'REA-001' LIMIT 1;
    SELECT Id INTO process_support_id FROM Processes WHERE OrganizationId = org_id AND Code = 'SUP-001' LIMIT 1;

    SELECT Id INTO qualite_user_id FROM Users WHERE Email = 'qualite@demo.local' LIMIT 1;
    SELECT Id INTO chef_user_id FROM Users WHERE Email = 'chef@demo.local' LIMIT 1;
    SELECT Id INTO user_demo_id FROM Users WHERE Email = 'user@demo.local' LIMIT 1;

    IF process_pilotage_id IS NOT NULL THEN
        INSERT INTO Procedures (
            OrganizationId, ProcessId, Code, Title, Objective, Scope, Description, ResponsibleUserId, Status, CreatedAt
        ) VALUES (
            org_id,
            process_pilotage_id,
            'PROC-001',
            'Procedure de pilotage strategique',
            'Structurer les revues de direction et le suivi des objectifs qualite.',
            'Direction generale, comite qualite, responsables processus',
            'Procedure cadre pour planifier, executer et tracer les activites de pilotage.',
            qualite_user_id,
            'ACTIF',
            NOW()
        ) ON CONFLICT (OrganizationId, Code) DO NOTHING;
    END IF;

    IF process_realisation_id IS NOT NULL THEN
        INSERT INTO Procedures (
            OrganizationId, ProcessId, Code, Title, Objective, Scope, Description, ResponsibleUserId, Status, CreatedAt
        ) VALUES (
            org_id,
            process_realisation_id,
            'PROC-002',
            'Procedure de traitement des inscriptions',
            'Garantir la conformite et la fluidite du parcours d''inscription.',
            'Scolarite, finance, accueil',
            'Procedure operationnelle de verification, validation et integration des dossiers.',
            chef_user_id,
            'ACTIF',
            NOW()
        ) ON CONFLICT (OrganizationId, Code) DO NOTHING;
    END IF;

    IF process_support_id IS NOT NULL THEN
        INSERT INTO Procedures (
            OrganizationId, ProcessId, Code, Title, Objective, Scope, Description, ResponsibleUserId, Status, CreatedAt
        ) VALUES (
            org_id,
            process_support_id,
            'PROC-003',
            'Procedure de support informatique',
            'Assurer la prise en charge rapide des incidents et demandes IT.',
            'Support utilisateurs, infrastructure, applications',
            'Procedure de qualification, priorisation et resolution des tickets informatiques.',
            user_demo_id,
            'ACTIF',
            NOW()
        ) ON CONFLICT (OrganizationId, Code) DO NOTHING;
    END IF;

    SELECT Id INTO procedure_1_id FROM Procedures WHERE OrganizationId = org_id AND Code = 'PROC-001' LIMIT 1;
    SELECT Id INTO procedure_2_id FROM Procedures WHERE OrganizationId = org_id AND Code = 'PROC-002' LIMIT 1;
    SELECT Id INTO procedure_3_id FROM Procedures WHERE OrganizationId = org_id AND Code = 'PROC-003' LIMIT 1;

    IF procedure_1_id IS NOT NULL THEN
        INSERT INTO Instructions (OrganizationId, ProcedureId, Code, Title, Description, Status, OrderIndex, CreatedAt)
        VALUES
            (org_id, procedure_1_id, 'INS-001-01', 'Planifier la revue de direction', 'Definir agenda, donnees d''entree et participants.', 'ACTIF', 1, NOW()),
            (org_id, procedure_1_id, 'INS-001-02', 'Conduire la revue et tracer les decisions', 'Animer la revue, statuer et formaliser les actions.', 'ACTIF', 2, NOW())
        ON CONFLICT (ProcedureId, Code) DO NOTHING;
    END IF;

    IF procedure_2_id IS NOT NULL THEN
        INSERT INTO Instructions (OrganizationId, ProcedureId, Code, Title, Description, Status, OrderIndex, CreatedAt)
        VALUES
            (org_id, procedure_2_id, 'INS-002-01', 'Verifier la completude du dossier', 'Controler les pieces et la conformite administrative.', 'ACTIF', 1, NOW()),
            (org_id, procedure_2_id, 'INS-002-02', 'Valider l''inscription', 'Enregistrer la validation et notifier les services concernes.', 'ACTIF', 2, NOW())
        ON CONFLICT (ProcedureId, Code) DO NOTHING;
    END IF;

    IF procedure_3_id IS NOT NULL THEN
        INSERT INTO Instructions (OrganizationId, ProcedureId, Code, Title, Description, Status, OrderIndex, CreatedAt)
        VALUES
            (org_id, procedure_3_id, 'INS-003-01', 'Qualifier le ticket', 'Identifier la criticite et assigner un niveau de priorite.', 'ACTIF', 1, NOW()),
            (org_id, procedure_3_id, 'INS-003-02', 'Resoudre et cloturer', 'Executer la resolution, valider et cloturer la demande.', 'ACTIF', 2, NOW())
        ON CONFLICT (ProcedureId, Code) DO NOTHING;
    END IF;
END $$;
