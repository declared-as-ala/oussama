-- PROCESS MODULE SETUP FOR POSTGRESQL
-- Compatible with current project (Dapper + Npgsql)

CREATE TABLE IF NOT EXISTS Processes (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    Code VARCHAR(50) NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    Type VARCHAR(30) NOT NULL CHECK (Type IN ('PILOTAGE', 'REALISATION', 'SUPPORT')),
    Finalities TEXT NULL,
    Scope TEXT NULL,
    Suppliers TEXT NULL,
    Clients TEXT NULL,
    InputData TEXT NULL,
    OutputData TEXT NULL,
    Objectives TEXT NULL,
    PilotUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK (Status IN ('ACTIF', 'INACTIF')),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL,
    CONSTRAINT uq_processes_org_code UNIQUE (OrganizationId, Code)
);

CREATE TABLE IF NOT EXISTS ProcessActors (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    ProcessId INTEGER NOT NULL REFERENCES Processes(Id) ON DELETE CASCADE,
    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    ActorType VARCHAR(30) NOT NULL CHECK (ActorType IN ('PILOTE', 'COPILOTE', 'CONTRIBUTEUR', 'OBSERVATEUR')),
    AssignedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_process_actors UNIQUE (ProcessId, UserId)
);

CREATE INDEX IF NOT EXISTS idx_processes_organization ON Processes(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_processes_code ON Processes(Code);
CREATE INDEX IF NOT EXISTS idx_processes_name ON Processes(Name);
CREATE INDEX IF NOT EXISTS idx_processes_type ON Processes(Type);
CREATE INDEX IF NOT EXISTS idx_processes_pilot ON Processes(PilotUserId);
CREATE INDEX IF NOT EXISTS idx_processes_status ON Processes(Status);
CREATE INDEX IF NOT EXISTS idx_processes_createdat ON Processes(CreatedAt);

CREATE INDEX IF NOT EXISTS idx_process_actors_org ON ProcessActors(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_process_actors_process ON ProcessActors(ProcessId);
CREATE INDEX IF NOT EXISTS idx_process_actors_user ON ProcessActors(UserId);
CREATE INDEX IF NOT EXISTS idx_process_actors_assigned ON ProcessActors(AssignedAt);

-- Demo data bound to organization DEMO when available
DO $$
DECLARE
    org_id INTEGER;
    qualite_user_id INTEGER;
    chef_user_id INTEGER;
    user_demo_id INTEGER;
    process_pilotage_id INTEGER;
    process_realisation_id INTEGER;
    process_support_id INTEGER;
BEGIN
    SELECT Id INTO org_id FROM Organizations WHERE Code = 'DEMO' LIMIT 1;

    IF org_id IS NULL THEN
        RAISE NOTICE 'Demo organization (DEMO) not found, process seed skipped.';
        RETURN;
    END IF;

    SELECT Id INTO qualite_user_id FROM Users WHERE Email = 'qualite@demo.local' LIMIT 1;
    SELECT Id INTO chef_user_id FROM Users WHERE Email = 'chef@demo.local' LIMIT 1;
    SELECT Id INTO user_demo_id FROM Users WHERE Email = 'user@demo.local' LIMIT 1;

    INSERT INTO Processes (
        OrganizationId, Code, Name, Description, Type, Finalities, Scope, Suppliers, Clients, InputData, OutputData, Objectives, PilotUserId, Status, CreatedAt
    ) VALUES (
        org_id,
        'PIL-001',
        'Pilotage strategique',
        'Pilotage global du systeme qualite et governance.',
        'PILOTAGE',
        '["Aligner les objectifs qualite", "Assurer la revue de direction"]',
        '["Toutes les directions", "Toutes les activites qualite"]',
        '["Direction generale", "Parties prenantes internes"]',
        '["Comite qualite", "Direction"]',
        '["Donnees de performance", "Retours qualite"]',
        '["Plan d''amelioration", "Decisions strategiques"]',
        '["Maintenir le taux de conformite > 90%"]',
        qualite_user_id,
        'ACTIF',
        NOW()
    ) ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO Processes (
        OrganizationId, Code, Name, Description, Type, Finalities, Scope, Suppliers, Clients, InputData, OutputData, Objectives, PilotUserId, Status, CreatedAt
    ) VALUES (
        org_id,
        'REA-001',
        'Gestion des inscriptions',
        'Processus de realisation pour l''inscription des apprenants.',
        'REALISATION',
        '["Fiabiliser le parcours d''inscription", "Reduire les delais"]',
        '["Service scolarite", "Service financier"]',
        '["Demandes d''inscription", "Documents candidats"]',
        '["Apprenants", "Directions pedagogiques"]',
        '["Dossiers candidats", "Pieces justificatives"]',
        '["Dossiers valides", "Planning d''integration"]',
        '["Delai moyen d''inscription < 48h"]',
        chef_user_id,
        'ACTIF',
        NOW()
    ) ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO Processes (
        OrganizationId, Code, Name, Description, Type, Finalities, Scope, Suppliers, Clients, InputData, OutputData, Objectives, PilotUserId, Status, CreatedAt
    ) VALUES (
        org_id,
        'SUP-001',
        'Support informatique',
        'Support technique et assistance utilisateurs.',
        'SUPPORT',
        '["Assurer la disponibilite des services IT", "Traiter les incidents"]',
        '["Infrastructure", "Applications metier"]',
        '["Equipe IT", "Prestataires techniques"]',
        '["Tous les collaborateurs"]',
        '["Tickets", "Alertes supervision"]',
        '["Incidents resolus", "Rapports de disponibilite"]',
        '["Taux de resolution > 95% sous 24h"]',
        user_demo_id,
        'ACTIF',
        NOW()
    ) ON CONFLICT (OrganizationId, Code) DO NOTHING;

    SELECT Id INTO process_pilotage_id FROM Processes WHERE OrganizationId = org_id AND Code = 'PIL-001' LIMIT 1;
    SELECT Id INTO process_realisation_id FROM Processes WHERE OrganizationId = org_id AND Code = 'REA-001' LIMIT 1;
    SELECT Id INTO process_support_id FROM Processes WHERE OrganizationId = org_id AND Code = 'SUP-001' LIMIT 1;

    IF process_pilotage_id IS NOT NULL AND qualite_user_id IS NOT NULL THEN
        INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
        VALUES (org_id, process_pilotage_id, qualite_user_id, 'PILOTE', NOW())
        ON CONFLICT (ProcessId, UserId) DO NOTHING;
    END IF;

    IF process_realisation_id IS NOT NULL AND chef_user_id IS NOT NULL THEN
        INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
        VALUES (org_id, process_realisation_id, chef_user_id, 'PILOTE', NOW())
        ON CONFLICT (ProcessId, UserId) DO NOTHING;
    END IF;

    IF process_support_id IS NOT NULL AND user_demo_id IS NOT NULL THEN
        INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
        VALUES (org_id, process_support_id, user_demo_id, 'PILOTE', NOW())
        ON CONFLICT (ProcessId, UserId) DO NOTHING;
    END IF;
END $$;
