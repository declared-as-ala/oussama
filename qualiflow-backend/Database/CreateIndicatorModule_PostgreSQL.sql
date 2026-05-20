-- INDICATOR / KPI MODULE SETUP FOR POSTGRESQL
-- Compatible with current project (Dapper + Npgsql)

CREATE TABLE IF NOT EXISTS Indicators (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    ProcessId INTEGER NOT NULL REFERENCES Processes(Id) ON DELETE RESTRICT,
    Code VARCHAR(50) NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    CalculationMethod TEXT NULL,
    Unit VARCHAR(50) NULL,
    TargetValue NUMERIC(18,4) NOT NULL DEFAULT 0,
    AlertThreshold NUMERIC(18,4) NOT NULL DEFAULT 0,
    MeasurementFrequency VARCHAR(20) NOT NULL DEFAULT 'MENSUEL' CHECK (MeasurementFrequency IN ('QUOTIDIEN', 'HEBDOMADAIRE', 'MENSUEL', 'TRIMESTRIEL', 'ANNUEL')),
    ResponsibleUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF' CHECK (Status IN ('ACTIF', 'INACTIF')),
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS IndicatorValues (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    IndicatorId INTEGER NOT NULL REFERENCES Indicators(Id) ON DELETE CASCADE,
    PeriodLabel VARCHAR(100) NOT NULL,
    MeasuredValue NUMERIC(18,4) NOT NULL,
    Comment TEXT NULL,
    MeasuredAt TIMESTAMP NOT NULL,
    EnteredByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS IndicatorAlerts (
    Id SERIAL PRIMARY KEY,
    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
    IndicatorId INTEGER NOT NULL REFERENCES Indicators(Id) ON DELETE CASCADE,
    IndicatorValueId INTEGER NOT NULL REFERENCES IndicatorValues(Id) ON DELETE CASCADE,
    AlertType VARCHAR(40) NOT NULL,
    Message TEXT NOT NULL,
    IsResolved BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ResolvedAt TIMESTAMP NULL
);

ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS Description TEXT NULL;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS CalculationMethod TEXT NULL;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS Unit VARCHAR(50) NULL;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS TargetValue NUMERIC(18,4) NOT NULL DEFAULT 0;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS AlertThreshold NUMERIC(18,4) NOT NULL DEFAULT 0;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS MeasurementFrequency VARCHAR(20) NOT NULL DEFAULT 'MENSUEL';
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS ResponsibleUserId INTEGER NULL;
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF';
ALTER TABLE Indicators ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMP NULL;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'indicators_status_check'
    ) THEN
        ALTER TABLE Indicators DROP CONSTRAINT indicators_status_check;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'indicators_measurementfrequency_check'
    ) THEN
        ALTER TABLE Indicators DROP CONSTRAINT indicators_measurementfrequency_check;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_indicators_status'
    ) THEN
        ALTER TABLE Indicators
        ADD CONSTRAINT ck_indicators_status
        CHECK (Status IN ('ACTIF', 'INACTIF'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_indicators_frequency'
    ) THEN
        ALTER TABLE Indicators
        ADD CONSTRAINT ck_indicators_frequency
        CHECK (MeasurementFrequency IN ('QUOTIDIEN', 'HEBDOMADAIRE', 'MENSUEL', 'TRIMESTRIEL', 'ANNUEL'));
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_indicators_responsible'
    ) THEN
        ALTER TABLE Indicators
        ADD CONSTRAINT fk_indicators_responsible
        FOREIGN KEY (ResponsibleUserId) REFERENCES Users(Id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS idx_indicators_org_code_unique ON Indicators(OrganizationId, Code);
CREATE UNIQUE INDEX IF NOT EXISTS idx_indicatorvalues_indicator_period_unique ON IndicatorValues(IndicatorId, PeriodLabel);

CREATE INDEX IF NOT EXISTS idx_indicators_org ON Indicators(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_indicators_process ON Indicators(ProcessId);
CREATE INDEX IF NOT EXISTS idx_indicators_responsible ON Indicators(ResponsibleUserId);
CREATE INDEX IF NOT EXISTS idx_indicators_status ON Indicators(Status);
CREATE INDEX IF NOT EXISTS idx_indicators_frequency ON Indicators(MeasurementFrequency);

CREATE INDEX IF NOT EXISTS idx_indicatorvalues_org ON IndicatorValues(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_indicatorvalues_indicator ON IndicatorValues(IndicatorId);
CREATE INDEX IF NOT EXISTS idx_indicatorvalues_measuredat ON IndicatorValues(MeasuredAt);
CREATE INDEX IF NOT EXISTS idx_indicatorvalues_enteredby ON IndicatorValues(EnteredByUserId);
CREATE INDEX IF NOT EXISTS idx_indicatorvalues_period ON IndicatorValues(PeriodLabel);

CREATE INDEX IF NOT EXISTS idx_indicatoralerts_org ON IndicatorAlerts(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_indicatoralerts_indicator ON IndicatorAlerts(IndicatorId);
CREATE INDEX IF NOT EXISTS idx_indicatoralerts_resolved ON IndicatorAlerts(IsResolved);
CREATE INDEX IF NOT EXISTS idx_indicatoralerts_createdat ON IndicatorAlerts(CreatedAt);

-- Demo seed data
DO $$
DECLARE
    org_id INTEGER;
    proc_pilotage INTEGER;
    proc_realisation INTEGER;
    proc_support INTEGER;
    qualite_user INTEGER;
    chef_user INTEGER;
    support_user INTEGER;
    ind_003_id INTEGER;
    ind_003_latest_value INTEGER;
BEGIN
    SELECT Id INTO org_id FROM Organizations WHERE Code = 'DEMO' LIMIT 1;

    IF org_id IS NULL THEN
        RAISE NOTICE 'Demo organization (DEMO) not found, KPI seed skipped.';
        RETURN;
    END IF;

    SELECT Id INTO proc_pilotage FROM Processes WHERE OrganizationId = org_id AND Code = 'PIL-001' LIMIT 1;
    SELECT Id INTO proc_realisation FROM Processes WHERE OrganizationId = org_id AND Code = 'REA-001' LIMIT 1;
    SELECT Id INTO proc_support FROM Processes WHERE OrganizationId = org_id AND Code = 'SUP-001' LIMIT 1;

    proc_pilotage := COALESCE(proc_pilotage, proc_realisation, proc_support);
    proc_realisation := COALESCE(proc_realisation, proc_pilotage, proc_support);
    proc_support := COALESCE(proc_support, proc_pilotage, proc_realisation);

    SELECT Id INTO qualite_user FROM Users WHERE Email = 'qualite@demo.local' LIMIT 1;
    SELECT Id INTO chef_user FROM Users WHERE Email = 'chef@demo.local' LIMIT 1;
    SELECT Id INTO support_user FROM Users WHERE Email = 'user@demo.local' LIMIT 1;

    IF proc_pilotage IS NULL THEN
        RAISE NOTICE 'No process found for KPI seed.';
        RETURN;
    END IF;

    INSERT INTO Indicators
        (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt)
    VALUES
        (org_id, proc_pilotage, 'IND-001', 'Taux de conformite documentaire',
         'Mesure le pourcentage de documents conformes lors des controles qualite.',
         '(Documents conformes / Documents controles) * 100',
         '%', 95, 90, 'MENSUEL', COALESCE(qualite_user, chef_user, 1), 'ACTIF', NOW())
    ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO Indicators
        (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt)
    VALUES
        (org_id, proc_realisation, 'IND-002', 'Delai moyen de traitement des non-conformites',
         'Indicateur de performance du traitement des non-conformites.',
         'Score de respect des delais de traitement des NC',
         'points', 85, 80, 'MENSUEL', COALESCE(chef_user, qualite_user, 1), 'ACTIF', NOW())
    ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO Indicators
        (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt)
    VALUES
        (org_id, proc_support, 'IND-003', 'Taux de realisation des actions correctives',
         'Pourcentage d''actions correctives executees dans les delais.',
         '(Actions realisees a temps / Actions planifiees) * 100',
         '%', 90, 85, 'MENSUEL', COALESCE(support_user, qualite_user, 1), 'ACTIF', NOW())
    ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO Indicators
        (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt)
    VALUES
        (org_id, proc_pilotage, 'IND-004', 'Taux de validation des procedures',
         'Suivi du taux de procedures validees conformement au planning.',
         '(Procedures validees / Procedures prevues) * 100',
         '%', 92, 88, 'TRIMESTRIEL', COALESCE(qualite_user, chef_user, 1), 'ACTIF', NOW())
    ON CONFLICT (OrganizationId, Code) DO NOTHING;

    INSERT INTO IndicatorValues
        (OrganizationId, IndicatorId, PeriodLabel, MeasuredValue, Comment, MeasuredAt, EnteredByUserId, CreatedAt)
    SELECT org_id, i.Id, seed.period_label, seed.measured_value, seed.comment, seed.measured_at, COALESCE(qualite_user, chef_user, support_user, 1), NOW()
    FROM Indicators i
    INNER JOIN (
        VALUES
            ('IND-001', '2025-11', 89::numeric, 'Lancement de campagne de mise a jour documentaire.', NOW() - INTERVAL '5 month'),
            ('IND-001', '2025-12', 91::numeric, 'Amelioration suite aux actions de controle.', NOW() - INTERVAL '4 month'),
            ('IND-001', '2026-01', 93::numeric, 'Consolidation des controles qualite.', NOW() - INTERVAL '3 month'),
            ('IND-001', '2026-02', 96::numeric, 'Objectif depasse ce mois-ci.', NOW() - INTERVAL '2 month'),
            ('IND-002', '2025-11', 78::numeric, 'Retards ponctuels sur le traitement des NC.', NOW() - INTERVAL '5 month'),
            ('IND-002', '2025-12', 80::numeric, 'Stabilisation du delai moyen.', NOW() - INTERVAL '4 month'),
            ('IND-002', '2026-01', 84::numeric, 'Amelioration progressive.', NOW() - INTERVAL '3 month'),
            ('IND-002', '2026-02', 86::numeric, 'Objectif atteint ce mois-ci.', NOW() - INTERVAL '2 month'),
            ('IND-003', '2025-11', 87::numeric, 'Suivi correct mais sous la cible.', NOW() - INTERVAL '5 month'),
            ('IND-003', '2025-12', 85::numeric, 'Niveau seuil atteint.', NOW() - INTERVAL '4 month'),
            ('IND-003', '2026-01', 83::numeric, 'Alerte sur baisse de performance.', NOW() - INTERVAL '3 month'),
            ('IND-003', '2026-02', 78::numeric, 'Performance insuffisante, plan d''action requis.', NOW() - INTERVAL '2 month'),
            ('IND-004', '2025-Q3', 89::numeric, 'Validation en progression.', NOW() - INTERVAL '8 month'),
            ('IND-004', '2025-Q4', 91::numeric, 'Resultat proche de la cible.', NOW() - INTERVAL '5 month'),
            ('IND-004', '2026-Q1', 93::numeric, 'Cible depassee sur le trimestre.', NOW() - INTERVAL '2 month')
    ) AS seed(indicator_code, period_label, measured_value, comment, measured_at)
        ON seed.indicator_code = i.Code
    WHERE i.OrganizationId = org_id
    ON CONFLICT (IndicatorId, PeriodLabel) DO NOTHING;

    SELECT Id INTO ind_003_id
    FROM Indicators
    WHERE OrganizationId = org_id AND Code = 'IND-003'
    LIMIT 1;

    IF ind_003_id IS NOT NULL THEN
        SELECT iv.Id INTO ind_003_latest_value
        FROM IndicatorValues iv
        WHERE iv.OrganizationId = org_id
          AND iv.IndicatorId = ind_003_id
        ORDER BY iv.MeasuredAt DESC, iv.Id DESC
        LIMIT 1;

        IF ind_003_latest_value IS NOT NULL THEN
            INSERT INTO IndicatorAlerts
                (OrganizationId, IndicatorId, IndicatorValueId, AlertType, Message, IsResolved, CreatedAt)
            SELECT
                org_id,
                ind_003_id,
                ind_003_latest_value,
                'BELOW_THRESHOLD',
                'Le taux de realisation des actions correctives est sous le seuil d''alerte.',
                FALSE,
                NOW()
            WHERE NOT EXISTS (
                SELECT 1
                FROM IndicatorAlerts
                WHERE OrganizationId = org_id
                  AND IndicatorId = ind_003_id
                  AND IndicatorValueId = ind_003_latest_value
                  AND IsResolved = FALSE
            );
        END IF;
    END IF;
END $$;
