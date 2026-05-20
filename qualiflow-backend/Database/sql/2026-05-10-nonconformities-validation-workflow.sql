ALTER TABLE NonConformities
    ALTER COLUMN Code DROP NOT NULL,
    ALTER COLUMN ResponsibleUserId DROP NOT NULL,
    ALTER COLUMN Status TYPE VARCHAR(30),
    ALTER COLUMN Status SET DEFAULT 'EN_ATTENTE_VALIDATION';

ALTER TABLE NonConformities
    DROP CONSTRAINT IF EXISTS nonconformities_status_check;

ALTER TABLE NonConformities
    ADD CONSTRAINT nonconformities_status_check
    CHECK (Status IN ('EN_ATTENTE_VALIDATION', 'OUVERTE', 'EN_COURS', 'CLOTUREE'));
