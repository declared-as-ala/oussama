using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BCryptNet = BCrypt.Net.BCrypt;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocApi.Infrastructure
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
        {
            using var scope = services.CreateScope();
            var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = connectionFactory.CreateConnection();

            const string schemaSql = @"
                CREATE TABLE IF NOT EXISTS Organizations (
                    Id SERIAL PRIMARY KEY,
                    Name VARCHAR(255) NOT NULL UNIQUE,
                    Code VARCHAR(50) NOT NULL UNIQUE,
                    Description TEXT NULL,
                    Type VARCHAR(100) NULL,
                    Address TEXT NULL,
                    Email VARCHAR(255) NULL,
                    Phone VARCHAR(30) NULL,
                    LogoPath TEXT NULL,
                    Status VARCHAR(20) NOT NULL DEFAULT 'ACTIF',
                    SubscriptionDaysRemaining INTEGER NOT NULL DEFAULT 30,
                    SubscriptionMonitorEnabled BOOLEAN NOT NULL DEFAULT TRUE,
                    LastSubscriptionDecrementAt TIMESTAMP NULL,
                    SubscriptionExpiryAlertSent BOOLEAN NOT NULL DEFAULT FALSE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL
                );

                ALTER TABLE Organizations ADD COLUMN IF NOT EXISTS LogoPath TEXT NULL;
                ALTER TABLE Organizations ADD COLUMN IF NOT EXISTS SubscriptionDaysRemaining INTEGER NOT NULL DEFAULT 30;
                ALTER TABLE Organizations ADD COLUMN IF NOT EXISTS SubscriptionMonitorEnabled BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE Organizations ADD COLUMN IF NOT EXISTS LastSubscriptionDecrementAt TIMESTAMP NULL;
                ALTER TABLE Organizations ADD COLUMN IF NOT EXISTS SubscriptionExpiryAlertSent BOOLEAN NOT NULL DEFAULT FALSE;

                CREATE TABLE IF NOT EXISTS Users (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NULL REFERENCES Organizations(Id) ON DELETE SET NULL,
                    FirstName VARCHAR(255) NOT NULL DEFAULT 'Unknown',
                    LastName VARCHAR(255) NOT NULL DEFAULT 'Unknown',
                    Email VARCHAR(255) NOT NULL,
                    Username VARCHAR(255) NULL,
                    PasswordHash VARCHAR(255) NOT NULL,
                    Role VARCHAR(50) NOT NULL DEFAULT 'UTILISATEUR',
                    Function VARCHAR(255) NULL,
                    Phone VARCHAR(30) NULL,
                    City VARCHAR(120) NULL,
                    BirthDate DATE NULL,
                    PreferredLanguage VARCHAR(10) NULL,
                    ProfilePhotoPath TEXT NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    IsEmailVerified BOOLEAN NOT NULL DEFAULT FALSE,
                    EmailVerificationToken VARCHAR(500) NULL,
                    EmailVerificationExpiresAt TIMESTAMP NULL,
                    PendingEmail VARCHAR(255) NULL,
                    EmailChangeVerificationToken VARCHAR(20) NULL,
                    EmailChangeVerificationExpiresAt TIMESTAMP NULL,
                    LastLoginAt TIMESTAMP NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL
                );

                ALTER TABLE Users ADD COLUMN IF NOT EXISTS OrganizationId INTEGER NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS FirstName VARCHAR(255) NOT NULL DEFAULT 'Unknown';
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS LastName VARCHAR(255) NOT NULL DEFAULT 'Unknown';
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS Email VARCHAR(255) NOT NULL DEFAULT '';
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS Username VARCHAR(255) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS PasswordHash VARCHAR(255) NOT NULL DEFAULT '';
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS Role VARCHAR(50) NOT NULL DEFAULT 'UTILISATEUR';
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS Function VARCHAR(255) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS Phone VARCHAR(30) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS City VARCHAR(120) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS BirthDate DATE NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS PreferredLanguage VARCHAR(10) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS ProfilePhotoPath TEXT NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS IsActive BOOLEAN NOT NULL DEFAULT TRUE;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS IsEmailVerified BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailVerificationToken VARCHAR(500) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailVerificationExpiresAt TIMESTAMP NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS PendingEmail VARCHAR(255) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailChangeVerificationToken VARCHAR(20) NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS EmailChangeVerificationExpiresAt TIMESTAMP NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS LastLoginAt TIMESTAMP NULL;
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS CreatedAt TIMESTAMP NOT NULL DEFAULT NOW();
                ALTER TABLE Users ADD COLUMN IF NOT EXISTS UpdatedAt TIMESTAMP NULL;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'fk_users_organization'
                    ) THEN
                        ALTER TABLE Users
                        ADD CONSTRAINT fk_users_organization
                        FOREIGN KEY (OrganizationId) REFERENCES Organizations(Id) ON DELETE SET NULL;
                    END IF;
                END $$;

                ALTER TABLE Users DROP CONSTRAINT IF EXISTS ""Users_Email_key"";
                DROP INDEX IF EXISTS idx_users_email_unique;
                CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_org_unique ON Users(Email, OrganizationId);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_superadmin_unique ON Users(Email) WHERE OrganizationId IS NULL;
                CREATE INDEX IF NOT EXISTS idx_users_org ON Users(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_users_active ON Users(IsActive);
                CREATE INDEX IF NOT EXISTS idx_users_role ON Users(Role);
                CREATE INDEX IF NOT EXISTS idx_users_created_at ON Users(CreatedAt);

                CREATE INDEX IF NOT EXISTS idx_org_name ON Organizations(Name);
                CREATE INDEX IF NOT EXISTS idx_org_code ON Organizations(Code);
                CREATE INDEX IF NOT EXISTS idx_org_status ON Organizations(Status);
                CREATE INDEX IF NOT EXISTS idx_org_type ON Organizations(Type);
                CREATE INDEX IF NOT EXISTS idx_org_subscription_days ON Organizations(SubscriptionDaysRemaining);
                CREATE INDEX IF NOT EXISTS idx_org_subscription_monitor_enabled ON Organizations(SubscriptionMonitorEnabled);
                CREATE INDEX IF NOT EXISTS idx_org_subscription_status_days ON Organizations(Status, SubscriptionDaysRemaining);

                CREATE TABLE IF NOT EXISTS RefreshTokens (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                    Token VARCHAR(500) NOT NULL UNIQUE,
                    ExpiresAt TIMESTAMP NOT NULL,
                    IsRevoked BOOLEAN NOT NULL DEFAULT FALSE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    RevokedAt TIMESTAMP NULL,
                    ReplacedByToken VARCHAR(500) NULL
                );

                CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user ON RefreshTokens(UserId);
                CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires ON RefreshTokens(ExpiresAt);
                CREATE INDEX IF NOT EXISTS idx_refresh_tokens_revoked ON RefreshTokens(IsRevoked);

                CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                    Token VARCHAR(500) NOT NULL UNIQUE,
                    ExpiresAt TIMESTAMP NOT NULL,
                    Used BOOLEAN NOT NULL DEFAULT FALSE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

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

                CREATE INDEX IF NOT EXISTS idx_processes_org ON Processes(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_processes_code ON Processes(Code);
                CREATE INDEX IF NOT EXISTS idx_processes_name ON Processes(Name);
                CREATE INDEX IF NOT EXISTS idx_processes_type ON Processes(Type);
                CREATE INDEX IF NOT EXISTS idx_processes_pilot ON Processes(PilotUserId);
                CREATE INDEX IF NOT EXISTS idx_processes_status ON Processes(Status);
                CREATE INDEX IF NOT EXISTS idx_processes_createdat ON Processes(CreatedAt);

                CREATE INDEX IF NOT EXISTS idx_processactors_org ON ProcessActors(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_processactors_process ON ProcessActors(ProcessId);
                CREATE INDEX IF NOT EXISTS idx_processactors_user ON ProcessActors(UserId);

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

                CREATE TABLE IF NOT EXISTS ProcessProcedures (
                    ProcessId INTEGER NOT NULL REFERENCES Processes(Id) ON DELETE CASCADE,
                    ProcedureId INTEGER NOT NULL REFERENCES Procedures(Id) ON DELETE CASCADE,
                    PRIMARY KEY (ProcessId, ProcedureId)
                );

                CREATE INDEX IF NOT EXISTS idx_processprocedures_process ON ProcessProcedures(ProcessId);
                CREATE INDEX IF NOT EXISTS idx_processprocedures_procedure ON ProcessProcedures(ProcedureId);

                INSERT INTO ProcessProcedures (ProcessId, ProcedureId)
                SELECT ProcessId, Id FROM Procedures
                ON CONFLICT (ProcessId, ProcedureId) DO NOTHING;

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

                CREATE TABLE IF NOT EXISTS Documents (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    ProcessId INTEGER NULL REFERENCES Processes(Id) ON DELETE SET NULL,
                    ProcedureId INTEGER NULL REFERENCES Procedures(Id) ON DELETE SET NULL,
                    Code VARCHAR(50) NOT NULL,
                    Title VARCHAR(255) NOT NULL,
                    Type VARCHAR(30) NOT NULL CHECK (Type IN ('MANUEL', 'PROCEDURE', 'ENREGISTREMENT', 'FORMULAIRE', 'INSTRUCTION', 'POLITIQUE', 'AUTRE')),
                    Description TEXT NULL,
                    Category VARCHAR(120) NULL,
                    Keywords TEXT NULL,
                    Signature TEXT NULL,
                    OwnerUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
                    CurrentVersionId INTEGER NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    DeletedAt TIMESTAMP NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL,
                    CONSTRAINT uq_documents_org_code UNIQUE (OrganizationId, Code)
                );

                CREATE TABLE IF NOT EXISTS DocumentVersions (
                    Id SERIAL PRIMARY KEY,
                    DocumentId INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    VersionNumber VARCHAR(30) NOT NULL,
                    Status VARCHAR(20) NOT NULL CHECK (Status IN ('BROUILLON', 'EN_REVISION', 'APPROUVE', 'PUBLIE', 'REJETE', 'PERIME', 'ARCHIVE')),
                    FileName VARCHAR(260) NULL,
                    OriginalFileName VARCHAR(260) NULL,
                    FilePath TEXT NULL,
                    FileExtension VARCHAR(20) NULL,
                    MimeType VARCHAR(150) NULL,
                    FileSize BIGINT NULL,
                    FileContent BYTEA NULL,
                    RevisionComment TEXT NULL,
                    EstablishedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    EstablishedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    VerifiedByUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
                    VerifiedAt TIMESTAMP NULL,
                    ValidatedByUserId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
                    ValidatedAt TIMESTAMP NULL,
                    EffectiveDate TIMESTAMP NULL,
                    ExpiryDate TIMESTAMP NULL,
                    IsCurrent BOOLEAN NOT NULL DEFAULT FALSE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL,
                    CONSTRAINT uq_documentversions_document_version UNIQUE (DocumentId, VersionNumber)
                );

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'fk_documents_currentversion'
                    ) THEN
                        ALTER TABLE Documents
                        ADD CONSTRAINT fk_documents_currentversion
                        FOREIGN KEY (CurrentVersionId) REFERENCES DocumentVersions(Id) ON DELETE SET NULL;
                    END IF;
                END $$;

                ALTER TABLE Documents ADD COLUMN IF NOT EXISTS DeletedAt TIMESTAMP NULL;
                ALTER TABLE Documents DROP CONSTRAINT IF EXISTS uq_documents_org_code;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_documents_org_code_active ON Documents(OrganizationId, Code) WHERE DeletedAt IS NULL;
                CREATE INDEX IF NOT EXISTS idx_documents_org ON Documents(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_documents_code ON Documents(Code);
                CREATE INDEX IF NOT EXISTS idx_documents_type ON Documents(Type);
                CREATE INDEX IF NOT EXISTS idx_documents_owner ON Documents(OwnerUserId);
                CREATE INDEX IF NOT EXISTS idx_documents_process ON Documents(ProcessId);
                CREATE INDEX IF NOT EXISTS idx_documents_procedure ON Documents(ProcedureId);
                CREATE INDEX IF NOT EXISTS idx_documents_currentversion ON Documents(CurrentVersionId);
                CREATE INDEX IF NOT EXISTS idx_documents_active ON Documents(IsActive);
                CREATE INDEX IF NOT EXISTS idx_documents_deletedat ON Documents(DeletedAt);
                CREATE INDEX IF NOT EXISTS idx_documents_updatedat ON Documents(UpdatedAt);

                ALTER TABLE Documents ADD COLUMN IF NOT EXISTS Signature TEXT NULL;
                ALTER TABLE Documents ADD COLUMN IF NOT EXISTS DeletedAt TIMESTAMP NULL;

                CREATE INDEX IF NOT EXISTS idx_documentversions_org ON DocumentVersions(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_documentversions_document ON DocumentVersions(DocumentId);
                CREATE INDEX IF NOT EXISTS idx_documentversions_status ON DocumentVersions(Status);
                CREATE INDEX IF NOT EXISTS idx_documentversions_current ON DocumentVersions(DocumentId, IsCurrent);
                CREATE INDEX IF NOT EXISTS idx_documentversions_established ON DocumentVersions(EstablishedAt);

                ALTER TABLE DocumentVersions ADD COLUMN IF NOT EXISTS FileContent BYTEA NULL;

                CREATE TABLE IF NOT EXISTS DocumentActionLogs (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    DocumentId INTEGER NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
                    DocumentVersionId INTEGER NULL REFERENCES DocumentVersions(Id) ON DELETE SET NULL,
                    ActionType VARCHAR(50) NOT NULL,
                    OldValue TEXT NULL,
                    NewValue TEXT NULL,
                    Comment TEXT NULL,
                    PerformedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    PerformedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_documentactionlogs_org ON DocumentActionLogs(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_documentactionlogs_document ON DocumentActionLogs(DocumentId);
                CREATE INDEX IF NOT EXISTS idx_documentactionlogs_version ON DocumentActionLogs(DocumentVersionId);
                CREATE INDEX IF NOT EXISTS idx_documentactionlogs_action ON DocumentActionLogs(ActionType);
                CREATE INDEX IF NOT EXISTS idx_documentactionlogs_performedat ON DocumentActionLogs(PerformedAt);

                CREATE TABLE IF NOT EXISTS ProcessActionLogs (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    ProcessId INTEGER NOT NULL REFERENCES Processes(Id) ON DELETE CASCADE,
                    ActionType VARCHAR(50) NOT NULL,
                    OldValue TEXT NULL,
                    NewValue TEXT NULL,
                    Comment TEXT NULL,
                    PerformedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    PerformedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_processactionlogs_org ON ProcessActionLogs(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_processactionlogs_process ON ProcessActionLogs(ProcessId);
                CREATE INDEX IF NOT EXISTS idx_processactionlogs_action ON ProcessActionLogs(ActionType);
                CREATE INDEX IF NOT EXISTS idx_processactionlogs_performedat ON ProcessActionLogs(PerformedAt);

                CREATE TABLE IF NOT EXISTS ProcedureActionLogs (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    ProcedureId INTEGER NOT NULL REFERENCES Procedures(Id) ON DELETE CASCADE,
                    ActionType VARCHAR(50) NOT NULL,
                    OldValue TEXT NULL,
                    NewValue TEXT NULL,
                    Comment TEXT NULL,
                    PerformedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    PerformedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_procedureactionlogs_org ON ProcedureActionLogs(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_procedureactionlogs_procedure ON ProcedureActionLogs(ProcedureId);
                CREATE INDEX IF NOT EXISTS idx_procedureactionlogs_action ON ProcedureActionLogs(ActionType);
                CREATE INDEX IF NOT EXISTS idx_procedureactionlogs_performedat ON ProcedureActionLogs(PerformedAt);

                CREATE TABLE IF NOT EXISTS NonConformities (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    Code VARCHAR(50) NOT NULL,
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

                CREATE TABLE IF NOT EXISTS NonConformityAttachments (
                    Id SERIAL PRIMARY KEY,
                    NonConformityId INTEGER NOT NULL REFERENCES NonConformities(Id) ON DELETE CASCADE,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    FileName VARCHAR(260) NOT NULL,
                    OriginalFileName VARCHAR(260) NOT NULL,
                    FileExtension VARCHAR(20) NULL,
                    MimeType VARCHAR(150) NULL,
                    FileSize BIGINT NULL,
                    FileContent BYTEA NOT NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_nonconformityattachments_nc ON NonConformityAttachments(NonConformityId);

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

                -- Rename CorrectiveActionHistories to CorrectiveActionActionLogs if it exists
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'correctiveactionhistories') AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'correctiveactionactionlogs') THEN
                        ALTER TABLE CorrectiveActionHistories RENAME TO CorrectiveActionActionLogs;
                    END IF;
                END $$;

                -- Create CorrectiveActionActionLogs table if it still doesn't exist
                CREATE TABLE IF NOT EXISTS CorrectiveActionActionLogs (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    CorrectiveActionId INTEGER NOT NULL REFERENCES CorrectiveActions(Id) ON DELETE CASCADE,
                    ActionType VARCHAR(50) NOT NULL,
                    OldValue TEXT NULL,
                    NewValue TEXT NULL,
                    Comment TEXT NULL,
                    PerformedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    PerformedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                -- Perform column and data migration for existing rows if table was renamed
                DO $$
                BEGIN
                    -- Rename columns if they still exist from the old schema
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'oldstatus') THEN
                        ALTER TABLE CorrectiveActionActionLogs RENAME COLUMN oldstatus TO OldValue;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'newstatus') THEN
                        ALTER TABLE CorrectiveActionActionLogs RENAME COLUMN newstatus TO NewValue;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'changedbyuserid') THEN
                        ALTER TABLE CorrectiveActionActionLogs RENAME COLUMN changedbyuserid TO PerformedByUserId;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'changedat') THEN
                        ALTER TABLE CorrectiveActionActionLogs RENAME COLUMN changedat TO PerformedAt;
                    END IF;

                    -- Alter columns types and nullability
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'oldvalue' AND data_type = 'character varying') THEN
                        ALTER TABLE CorrectiveActionActionLogs ALTER COLUMN OldValue TYPE TEXT;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'newvalue' AND data_type = 'character varying') THEN
                        ALTER TABLE CorrectiveActionActionLogs ALTER COLUMN NewValue TYPE TEXT;
                        ALTER TABLE CorrectiveActionActionLogs ALTER COLUMN NewValue DROP NOT NULL;
                    END IF;

                    -- Add ActionType if it doesn't exist
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'correctiveactionactionlogs' AND column_name = 'actiontype') THEN
                        ALTER TABLE CorrectiveActionActionLogs ADD COLUMN ActionType VARCHAR(50);
                        -- Backfill existing records with a default action type
                        UPDATE CorrectiveActionActionLogs SET ActionType = 'STATUS_CHANGED' WHERE ActionType IS NULL;
                        ALTER TABLE CorrectiveActionActionLogs ALTER COLUMN ActionType SET NOT NULL;
                    END IF;
                END $$;

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
                CREATE INDEX IF NOT EXISTS idx_correctiveactionactionlogs_action ON CorrectiveActionActionLogs(CorrectiveActionId);
                CREATE INDEX IF NOT EXISTS idx_correctiveactionactionlogs_org ON CorrectiveActionActionLogs(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_correctiveactionactionlogs_performedat ON CorrectiveActionActionLogs(PerformedAt);

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
                    UpdatedAt TIMESTAMP NULL,
                    CONSTRAINT uq_indicators_org_code UNIQUE (OrganizationId, Code)
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
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    CONSTRAINT uq_indicatorvalues_indicator_period UNIQUE (IndicatorId, PeriodLabel)
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
                END $$;

                CREATE UNIQUE INDEX IF NOT EXISTS idx_indicators_org_code_unique ON Indicators(OrganizationId, Code);
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

                CREATE TABLE IF NOT EXISTS Notifications (
                    Id SERIAL PRIMARY KEY,
                    PublicId UUID NULL,
                    OrganizationId INTEGER NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                    SenderId INTEGER NULL REFERENCES Users(Id) ON DELETE SET NULL,
                    Type VARCHAR(80) NOT NULL,
                    Category VARCHAR(20) NOT NULL CHECK (Category IN ('INFO', 'SUCCESS', 'WARNING', 'ERROR')),
                    Title VARCHAR(255) NOT NULL,
                    Message TEXT NOT NULL,
                    Priority VARCHAR(20) NOT NULL DEFAULT 'MEDIUM' CHECK (Priority IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
                    IsRead BOOLEAN NOT NULL DEFAULT FALSE,
                    ReadAt TIMESTAMP NULL,
                    IsPushSent BOOLEAN NOT NULL DEFAULT FALSE,
                    Channel VARCHAR(20) NOT NULL DEFAULT 'INAPP',
                    ExternalProviderId VARCHAR(255) NULL,
                    IsArchived BOOLEAN NOT NULL DEFAULT FALSE,
                    DocumentId INTEGER NULL,
                    EntityType VARCHAR(100) NULL,
                    EntityId INTEGER NULL,
                    SourceModule VARCHAR(100) NULL,
                    RedirectUrl VARCHAR(500) NULL,
                    ExpiresAt TIMESTAMP NULL,
                    ReferenceType VARCHAR(80) NULL,
                    ReferenceId VARCHAR(80) NULL,
                    ActionUrl VARCHAR(500) NULL,
                    TargetRole VARCHAR(80) NULL,
                    EmailSent BOOLEAN NOT NULL DEFAULT FALSE,
                    EmailSentAt TIMESTAMP NULL,
                    EmailError TEXT NULL,
                    EmailAttemptCount INTEGER NOT NULL DEFAULT 0,
                    EmailNextAttemptAt TIMESTAMP NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE TABLE IF NOT EXISTS UserDevices (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                    DeviceToken TEXT NOT NULL,
                    Platform VARCHAR(20) NOT NULL,
                    DeviceName VARCHAR(255) NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    LastSeenAt TIMESTAMP NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_userdevices_user_token_unique ON UserDevices(UserId, DeviceToken);
                CREATE INDEX IF NOT EXISTS idx_userdevices_user ON UserDevices(UserId);
                CREATE INDEX IF NOT EXISTS idx_userdevices_active ON UserDevices(IsActive);

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

                CREATE TABLE IF NOT EXISTS AlertRules (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    Code VARCHAR(60) NOT NULL,
                    Name VARCHAR(255) NOT NULL,
                    Description TEXT NULL,
                    EntityType VARCHAR(80) NOT NULL,
                    TriggerType VARCHAR(80) NOT NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    ThresholdValue NUMERIC(12,2) NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL
                );

                CREATE TABLE IF NOT EXISTS NotificationPreferences (
                    Id SERIAL PRIMARY KEY,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                    NotificationType VARCHAR(80) NOT NULL,
                    InAppEnabled BOOLEAN NOT NULL DEFAULT TRUE,
                    EmailEnabled BOOLEAN NOT NULL DEFAULT FALSE,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
                    UpdatedAt TIMESTAMP NULL,
                    CONSTRAINT uq_notificationpreferences_user_type UNIQUE (UserId, NotificationType)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_alertrules_org_code_unique ON AlertRules(OrganizationId, Code);
                WITH duplicate_notifications AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY UserId, Type, COALESCE(ReferenceType, ''), COALESCE(ReferenceId, '')
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS RowNumber
                    FROM Notifications
                    WHERE IsArchived = FALSE
                )
                DELETE FROM Notifications
                WHERE Id IN (
                    SELECT Id
                    FROM duplicate_notifications
                    WHERE RowNumber > 1
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_notifications_active_dedupe_unique
                    ON Notifications(UserId, Type, (COALESCE(ReferenceType, '')), (COALESCE(ReferenceId, '')))
                    WHERE IsArchived = FALSE;

                CREATE INDEX IF NOT EXISTS idx_notifications_user ON Notifications(UserId);
                CREATE INDEX IF NOT EXISTS idx_notifications_org ON Notifications(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_notifications_read ON Notifications(IsRead);
                CREATE INDEX IF NOT EXISTS idx_notifications_priority ON Notifications(Priority);
                CREATE INDEX IF NOT EXISTS idx_notifications_type ON Notifications(Type);
                CREATE INDEX IF NOT EXISTS idx_notifications_archived ON Notifications(IsArchived);
                CREATE INDEX IF NOT EXISTS idx_notifications_created ON Notifications(CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_notifications_push_sent ON Notifications(IsPushSent);
                CREATE INDEX IF NOT EXISTS idx_notifications_channel ON Notifications(Channel);
                CREATE INDEX IF NOT EXISTS idx_notifications_document_id ON Notifications(DocumentId);
                CREATE INDEX IF NOT EXISTS idx_notifications_email_sent ON Notifications(EmailSent);
                CREATE INDEX IF NOT EXISTS idx_notifications_org_targetrole_active ON Notifications(OrganizationId, TargetRole, EmailSent);
                CREATE INDEX IF NOT EXISTS idx_notifications_email_pending_created ON Notifications(EmailSent, CreatedAt);
                CREATE INDEX IF NOT EXISTS idx_notifications_email_retry ON Notifications(EmailSent, EmailNextAttemptAt, CreatedAt);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_notifications_public_id_unique ON Notifications(PublicId) WHERE PublicId IS NOT NULL;
                CREATE INDEX IF NOT EXISTS idx_notificationpreferences_user ON NotificationPreferences(UserId);

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

                CREATE TABLE IF NOT EXISTS ActionLogs (
                    Id SERIAL PRIMARY KEY,
                    OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id) ON DELETE CASCADE,
                    Module VARCHAR(100) NOT NULL,
                    ActionType VARCHAR(100) NOT NULL,
                    Title VARCHAR(255) NOT NULL,
                    Description TEXT NULL,
                    PerformedByUserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    ActorName VARCHAR(255) NULL,
                    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_actionlogs_org ON ActionLogs(OrganizationId);
                CREATE INDEX IF NOT EXISTS idx_actionlogs_module ON ActionLogs(Module);
                CREATE INDEX IF NOT EXISTS idx_actionlogs_action ON ActionLogs(ActionType);
                CREATE INDEX IF NOT EXISTS idx_actionlogs_createdat ON ActionLogs(CreatedAt);
            ";

            await connection.ExecuteAsync(schemaSql);
            await connection.ExecuteAsync(@"
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
                END $$;");
            
            await connection.ExecuteAsync("ALTER TABLE Documents ADD COLUMN IF NOT EXISTS Signature TEXT NULL;");
            await connection.ExecuteAsync("ALTER TABLE DocumentVersions ADD COLUMN IF NOT EXISTS Signature TEXT NULL;");
            
            await connection.ExecuteAsync("ALTER TABLE Processes ADD COLUMN IF NOT EXISTS VersionNumber VARCHAR(30) NOT NULL DEFAULT '1.0';");
            await connection.ExecuteAsync("ALTER TABLE Processes ADD COLUMN IF NOT EXISTS RevisionComment TEXT NULL;");
            await connection.ExecuteAsync("ALTER TABLE Procedures ADD COLUMN IF NOT EXISTS VersionNumber VARCHAR(30) NOT NULL DEFAULT '1.0';");
            await connection.ExecuteAsync("ALTER TABLE Procedures ADD COLUMN IF NOT EXISTS RevisionComment TEXT NULL;");
            
            logger.LogInformation("Database schema ensured.");

            var enableDemoAccounts = configuration.GetValue("DemoAccounts:EnableDemoAccounts", true);
            if (!enableDemoAccounts)
            {
                return;
            }

            const string createDemoOrgSql = @"
                INSERT INTO Organizations (Name, Code, Description, Type, Status, CreatedAt)
                VALUES (@Name, @Code, @Description, @Type, @Status, NOW())
                ON CONFLICT (Code) DO NOTHING;
            ";

            await connection.ExecuteAsync(createDemoOrgSql, new
            {
                Name = "Demo Organization",
                Code = "DEMO",
                Description = "Demo organization for local development",
                Type = "Test",
                Status = "ACTIF"
            });

            await connection.ExecuteAsync(createDemoOrgSql, new
            {
                Name = "Institut Demo Nord",
                Code = "INST-NORD",
                Description = "Demo institute (north region)",
                Type = "INSTITUT",
                Status = "ACTIF"
            });

            await connection.ExecuteAsync(createDemoOrgSql, new
            {
                Name = "Centre Demo Sud",
                Code = "CENTRE-SUD",
                Description = "Demo center (south region)",
                Type = "CENTRE",
                Status = "ACTIF"
            });

            await connection.ExecuteAsync(createDemoOrgSql, new
            {
                Name = "Entreprise Demo",
                Code = "ENT-DEMO",
                Description = "Demo enterprise organization",
                Type = "ENTREPRISE",
                Status = "SUSPENDUE"
            });

            var organizationId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Organizations WHERE Code = @Code",
                new { Code = "DEMO" });

            var organizationNordId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Organizations WHERE Code = @Code",
                new { Code = "INST-NORD" });

            var organizationSudId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Organizations WHERE Code = @Code",
                new { Code = "CENTRE-SUD" });

            if (!organizationId.HasValue)
            {
                logger.LogWarning("Demo organization not found after initialization.");
                return;
            }

            var demoUsers = new List<DemoUser>
            {
                new("superadmin@demo.local", "SuperAdmin@123", "Super", "Admin", null, "SUPER_ADMIN", "System Administrator"),
                new("admin@demo.local", "Admin@123", "Admin", "Demo", organizationId, "ADMIN_ORG", "Organization Administrator"),
                new("qualite@demo.local", "Qualite@123", "Qualite", "Manager", organizationId, "RESPONSABLE_QUALITE", "Quality Manager"),
                new("user@demo.local", "User@123", "User", "Demo", organizationId, "UTILISATEUR", "Standard User"),
                new("admin.nord@demo.local", "AdminNord@123", "Admin", "Nord", organizationNordId, "ADMIN_ORG", "Organization Administrator"),
                new("admin.sud@demo.local", "AdminSud@123", "Admin", "Sud", organizationSudId, "ADMIN_ORG", "Organization Administrator")
            };

            const string userExistsSql = @"
                SELECT 1
                FROM Users
                WHERE Email = @Email
                  AND (
                    (@OrganizationId IS NULL AND OrganizationId IS NULL) OR
                    (@OrganizationId IS NOT NULL AND OrganizationId = @OrganizationId)
                  )
                LIMIT 1";
            const string insertUserSql = @"
                INSERT INTO Users
                    (OrganizationId, FirstName, LastName, Email, Username, PasswordHash, Role, Function, IsActive, IsEmailVerified, EmailVerificationToken, EmailVerificationExpiresAt, CreatedAt)
                VALUES
                    (@OrganizationId, @FirstName, @LastName, @Email, @Username, @PasswordHash, @Role, @Function, TRUE, TRUE, NULL, NULL, NOW())
                ON CONFLICT (Email, OrganizationId) DO NOTHING;
            ";

            foreach (var demoUser in demoUsers)
            {
                var exists = await connection.QueryFirstOrDefaultAsync<int?>(userExistsSql, new { demoUser.Email, demoUser.OrganizationId });
                if (exists.HasValue)
                {
                    continue;
                }

                var passwordHash = BCryptNet.HashPassword(demoUser.Password, workFactor: 11);

                await connection.ExecuteAsync(insertUserSql, new
                {
                    demoUser.OrganizationId,
                    demoUser.FirstName,
                    demoUser.LastName,
                    demoUser.Email,
                    Username = demoUser.Email,
                    PasswordHash = passwordHash,
                    demoUser.Role,
                    Function = demoUser.JobFunction
                });
            }

            // Keep demo accounts usable even after enabling email-verification on existing databases.
            await connection.ExecuteAsync(
                @"UPDATE Users
                  SET IsEmailVerified = TRUE,
                      EmailVerificationToken = NULL,
                      EmailVerificationExpiresAt = NULL,
                      UpdatedAt = NOW()
                  WHERE Email = ANY(@Emails);",
                new
                {
                    Emails = demoUsers.Select(user => user.Email).ToArray()
                });

            var pilotQualiteId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Users WHERE Email = @Email LIMIT 1",
                new { Email = "qualite@demo.local" });

            var pilotChefId = pilotQualiteId;

            var pilotSupportId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Users WHERE Email = @Email LIMIT 1",
                new { Email = "user@demo.local" });

            const string insertProcessSql = @"
                INSERT INTO Processes
                    (OrganizationId, Code, Name, Description, Type, Finalities, Scope, Suppliers, Clients, InputData, OutputData, Objectives, PilotUserId, Status, CreatedAt)
                VALUES
                    (@OrganizationId, @Code, @Name, @Description, @Type, @Finalities, @Scope, @Suppliers, @Clients, @InputData, @OutputData, @Objectives, @PilotUserId, @Status, NOW())
                ON CONFLICT (OrganizationId, Code) DO NOTHING;
            ";

            await connection.ExecuteAsync(insertProcessSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "PIL-001",
                Name = "Pilotage strategique",
                Description = "Pilotage global du systeme qualite et governance.",
                Type = "PILOTAGE",
                Finalities = "[\"Aligner les objectifs qualite\", \"Assurer la revue de direction\"]",
                Scope = "[\"Toutes les directions\", \"Toutes les activites qualite\"]",
                Suppliers = "[\"Direction generale\", \"Parties prenantes internes\"]",
                Clients = "[\"Comite qualite\", \"Direction\"]",
                InputData = "[\"Donnees de performance\", \"Retours qualite\"]",
                OutputData = "[\"Plan d'amelioration\", \"Decisions strategiques\"]",
                Objectives = "[\"Maintenir le taux de conformite > 90%\"]",
                PilotUserId = pilotQualiteId,
                Status = "ACTIF"
            });

            await connection.ExecuteAsync(insertProcessSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "REA-001",
                Name = "Gestion des inscriptions",
                Description = "Processus de realisation pour l'inscription des apprenants.",
                Type = "REALISATION",
                Finalities = "[\"Fiabiliser le parcours d'inscription\", \"Reduire les delais\"]",
                Scope = "[\"Service scolarite\", \"Service financier\"]",
                Suppliers = "[\"Demandes d'inscription\", \"Documents candidats\"]",
                Clients = "[\"Apprenants\", \"Directions pedagogiques\"]",
                InputData = "[\"Dossiers candidats\", \"Pieces justificatives\"]",
                OutputData = "[\"Dossiers valides\", \"Planning d'integration\"]",
                Objectives = "[\"Delai moyen d'inscription < 48h\"]",
                PilotUserId = pilotChefId,
                Status = "ACTIF"
            });

            await connection.ExecuteAsync(insertProcessSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "SUP-001",
                Name = "Support informatique",
                Description = "Support technique et assistance utilisateurs.",
                Type = "SUPPORT",
                Finalities = "[\"Assurer la disponibilite des services IT\", \"Traiter les incidents\"]",
                Scope = "[\"Infrastructure\", \"Applications metier\"]",
                Suppliers = "[\"Equipe IT\", \"Prestataires techniques\"]",
                Clients = "[\"Tous les collaborateurs\"]",
                InputData = "[\"Tickets\", \"Alertes supervision\"]",
                OutputData = "[\"Incidents resolus\", \"Rapports de disponibilite\"]",
                Objectives = "[\"Taux de resolution > 95% sous 24h\"]",
                PilotUserId = pilotSupportId,
                Status = "ACTIF"
            });

            const string processActorSql = @"
                INSERT INTO ProcessActors (OrganizationId, ProcessId, UserId, ActorType, AssignedAt)
                VALUES (@OrganizationId, @ProcessId, @UserId, @ActorType, NOW())
                ON CONFLICT (ProcessId, UserId) DO NOTHING;
            ";

            var pilotageId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Processes WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "PIL-001" });

            var realisationId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Processes WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "REA-001" });

            var supportId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Processes WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "SUP-001" });

            if (pilotageId.HasValue && pilotQualiteId.HasValue)
            {
                await connection.ExecuteAsync(processActorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = pilotageId.Value,
                    UserId = pilotQualiteId.Value,
                    ActorType = "PILOTE"
                });
            }

            if (realisationId.HasValue && pilotChefId.HasValue)
            {
                await connection.ExecuteAsync(processActorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = realisationId.Value,
                    UserId = pilotChefId.Value,
                    ActorType = "PILOTE"
                });
            }

            if (supportId.HasValue && pilotSupportId.HasValue)
            {
                await connection.ExecuteAsync(processActorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = supportId.Value,
                    UserId = pilotSupportId.Value,
                    ActorType = "PILOTE"
                });
            }

            const string insertProcedureSql = @"
                INSERT INTO Procedures
                    (OrganizationId, ProcessId, Code, Title, Objective, Scope, Description, ResponsibleUserId, Status, CreatedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @Code, @Title, @Objective, @Scope, @Description, @ResponsibleUserId, @Status, NOW())
                ON CONFLICT (OrganizationId, Code) DO NOTHING;
            ";

            if (pilotageId.HasValue)
            {
                await connection.ExecuteAsync(insertProcedureSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = pilotageId.Value,
                    Code = "PROC-001",
                    Title = "Procedure de pilotage strategique",
                    Objective = "Structurer les revues de direction et le suivi des objectifs qualite.",
                    Scope = "Direction generale, comite qualite, responsables processus",
                    Description = "Procedure cadre pour planifier, executer et tracer les activites de pilotage.",
                    ResponsibleUserId = pilotQualiteId,
                    Status = "ACTIF"
                });
            }

            if (realisationId.HasValue)
            {
                await connection.ExecuteAsync(insertProcedureSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = realisationId.Value,
                    Code = "PROC-002",
                    Title = "Procedure de traitement des inscriptions",
                    Objective = "Garantir la conformite et la fluidite du parcours d'inscription.",
                    Scope = "Scolarite, finance, accueil",
                    Description = "Procedure operationnelle de verification, validation et integration des dossiers.",
                    ResponsibleUserId = pilotChefId,
                    Status = "ACTIF"
                });
            }

            if (supportId.HasValue)
            {
                await connection.ExecuteAsync(insertProcedureSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = supportId.Value,
                    Code = "PROC-003",
                    Title = "Procedure de support informatique",
                    Objective = "Assurer la prise en charge rapide des incidents et demandes IT.",
                    Scope = "Support utilisateurs, infrastructure, applications",
                    Description = "Procedure de qualification, priorisation et resolution des tickets informatiques.",
                    ResponsibleUserId = pilotSupportId,
                    Status = "ACTIF"
                });
            }

            const string insertInstructionSql = @"
                INSERT INTO Instructions
                    (OrganizationId, ProcedureId, Code, Title, Description, Status, OrderIndex, CreatedAt)
                VALUES
                    (@OrganizationId, @ProcedureId, @Code, @Title, @Description, @Status, @OrderIndex, NOW())
                ON CONFLICT (ProcedureId, Code) DO NOTHING;
            ";

            var procedure1Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Procedures WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "PROC-001" });

            var procedure2Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Procedures WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "PROC-002" });

            var procedure3Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Procedures WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "PROC-003" });

            if (procedure1Id.HasValue)
            {
                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure1Id.Value,
                    Code = "INS-001-01",
                    Title = "Planifier la revue de direction",
                    Description = "Definir agenda, donnees d'entree et participants.",
                    Status = "ACTIF",
                    OrderIndex = 1
                });

                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure1Id.Value,
                    Code = "INS-001-02",
                    Title = "Conduire la revue et tracer les decisions",
                    Description = "Animer la revue, statuer et formaliser les actions.",
                    Status = "ACTIF",
                    OrderIndex = 2
                });
            }

            if (procedure2Id.HasValue)
            {
                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure2Id.Value,
                    Code = "INS-002-01",
                    Title = "Verifier la completude du dossier",
                    Description = "Controler les pieces et la conformite administrative.",
                    Status = "ACTIF",
                    OrderIndex = 1
                });

                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure2Id.Value,
                    Code = "INS-002-02",
                    Title = "Valider l'inscription",
                    Description = "Enregistrer la validation et notifier les services concernes.",
                    Status = "ACTIF",
                    OrderIndex = 2
                });
            }

            if (procedure3Id.HasValue)
            {
                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure3Id.Value,
                    Code = "INS-003-01",
                    Title = "Qualifier le ticket",
                    Description = "Identifier la criticite et assigner un niveau de priorite.",
                    Status = "ACTIF",
                    OrderIndex = 1
                });

                await connection.ExecuteAsync(insertInstructionSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcedureId = procedure3Id.Value,
                    Code = "INS-003-02",
                    Title = "Resoudre et cloturer",
                    Description = "Executer la resolution, valider et cloturer la demande.",
                    Status = "ACTIF",
                    OrderIndex = 2
                });
            }

            var storageRoot = ResolveStorageRootPath(configuration["Storage:RootPath"]);
            Directory.CreateDirectory(storageRoot);

            const string insertDocumentSql = @"
                INSERT INTO Documents
                    (OrganizationId, ProcessId, ProcedureId, Code, Title, Type, Description, Category, Keywords, Signature, OwnerUserId, IsActive, CreatedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @ProcedureId, @Code, @Title, @Type, @Description, @Category, @Keywords, NULL, @OwnerUserId, @IsActive, NOW())
                ON CONFLICT (OrganizationId, Code) WHERE DeletedAt IS NULL DO NOTHING;
            ";

            await connection.ExecuteAsync(insertDocumentSql, new
            {
                OrganizationId = organizationId.Value,
                ProcessId = pilotageId,
                ProcedureId = procedure1Id,
                Code = "DOC-MAN-001",
                Title = "Manuel qualite",
                Type = "MANUEL",
                Description = "Document de reference du systeme de management de la qualite.",
                Category = "SMQ",
                Keywords = "qualite,manuel,iso",
                OwnerUserId = pilotQualiteId,
                IsActive = true
            });

            await connection.ExecuteAsync(insertDocumentSql, new
            {
                OrganizationId = organizationId.Value,
                ProcessId = realisationId,
                ProcedureId = procedure2Id,
                Code = "DOC-PROC-001",
                Title = "Procedure d'inscription",
                Type = "PROCEDURE",
                Description = "Procedure complete de traitement et validation des inscriptions.",
                Category = "Operations",
                Keywords = "procedure,inscription",
                OwnerUserId = pilotChefId,
                IsActive = true
            });

            await connection.ExecuteAsync(insertDocumentSql, new
            {
                OrganizationId = organizationId.Value,
                ProcessId = realisationId,
                ProcedureId = procedure2Id,
                Code = "DOC-ENR-001",
                Title = "Fiche de presence",
                Type = "ENREGISTREMENT",
                Description = "Modele d'enregistrement de presence des apprenants.",
                Category = "Enregistrements",
                Keywords = "presence,enregistrement",
                OwnerUserId = pilotChefId,
                IsActive = true
            });

            await connection.ExecuteAsync(insertDocumentSql, new
            {
                OrganizationId = organizationId.Value,
                ProcessId = supportId,
                ProcedureId = procedure3Id,
                Code = "DOC-FORM-001",
                Title = "Formulaire de reclamation",
                Type = "FORMULAIRE",
                Description = "Formulaire standard de reclamation qualite et support.",
                Category = "Formulaires",
                Keywords = "formulaire,reclamation",
                OwnerUserId = pilotSupportId,
                IsActive = true
            });

            const string selectDocumentIdSql = @"
                SELECT Id
                FROM Documents
                WHERE OrganizationId = @OrganizationId AND Code = @Code
                LIMIT 1;";

            var manualDocumentId = await connection.QueryFirstOrDefaultAsync<int?>(selectDocumentIdSql, new { OrganizationId = organizationId.Value, Code = "DOC-MAN-001" });
            var procedureDocumentId = await connection.QueryFirstOrDefaultAsync<int?>(selectDocumentIdSql, new { OrganizationId = organizationId.Value, Code = "DOC-PROC-001" });
            var recordDocumentId = await connection.QueryFirstOrDefaultAsync<int?>(selectDocumentIdSql, new { OrganizationId = organizationId.Value, Code = "DOC-ENR-001" });
            var formDocumentId = await connection.QueryFirstOrDefaultAsync<int?>(selectDocumentIdSql, new { OrganizationId = organizationId.Value, Code = "DOC-FORM-001" });

            const string insertDocumentVersionSql = @"
                INSERT INTO DocumentVersions
                    (DocumentId, OrganizationId, VersionNumber, Status, FileName, OriginalFileName, FilePath, FileExtension, MimeType, FileSize, RevisionComment, EstablishedByUserId, EstablishedAt, VerifiedByUserId, VerifiedAt, ValidatedByUserId, ValidatedAt, EffectiveDate, ExpiryDate, IsCurrent, CreatedAt)
                VALUES
                    (@DocumentId, @OrganizationId, @VersionNumber, @Status, @FileName, @OriginalFileName, @FilePath, @FileExtension, @MimeType, @FileSize, @RevisionComment, @EstablishedByUserId, NOW(), @VerifiedByUserId, @VerifiedAt, @ValidatedByUserId, @ValidatedAt, @EffectiveDate, @ExpiryDate, TRUE, NOW())
                ON CONFLICT (DocumentId, VersionNumber) DO NOTHING;
            ";

            if (manualDocumentId.HasValue)
            {
                var file = EnsureDemoDocumentFile(storageRoot, organizationId.Value, "DOC-MAN-001", "v3.2", "manuel_qualite_v3_2.txt", "Manuel qualite - version 3.2");
                await connection.ExecuteAsync(insertDocumentVersionSql, new
                {
                    DocumentId = manualDocumentId.Value,
                    OrganizationId = organizationId.Value,
                    VersionNumber = "v3.2",
                    Status = "APPROUVE",
                    FileName = file.FileName,
                    OriginalFileName = file.OriginalFileName,
                    FilePath = file.RelativePath,
                    FileExtension = file.FileExtension,
                    MimeType = "text/plain",
                    FileSize = file.FileSize,
                    RevisionComment = "Version de reference validee.",
                    EstablishedByUserId = pilotQualiteId ?? 1,
                    VerifiedByUserId = pilotQualiteId ?? 1,
                    VerifiedAt = DateTime.UtcNow.AddDays(-20),
                    ValidatedByUserId = pilotQualiteId ?? 1,
                    ValidatedAt = DateTime.UtcNow.AddDays(-18),
                    EffectiveDate = DateTime.UtcNow.AddDays(-18),
                    ExpiryDate = (DateTime?)null
                });
            }

            if (procedureDocumentId.HasValue)
            {
                var file = EnsureDemoDocumentFile(storageRoot, organizationId.Value, "DOC-PROC-001", "v2.0", "procedure_inscription_v2_0.txt", "Procedure d'inscription - version 2.0");
                await connection.ExecuteAsync(insertDocumentVersionSql, new
                {
                    DocumentId = procedureDocumentId.Value,
                    OrganizationId = organizationId.Value,
                    VersionNumber = "v2.0",
                    Status = "EN_REVISION",
                    FileName = file.FileName,
                    OriginalFileName = file.OriginalFileName,
                    FilePath = file.RelativePath,
                    FileExtension = file.FileExtension,
                    MimeType = "text/plain",
                    FileSize = file.FileSize,
                    RevisionComment = "Version en cours de relecture.",
                    EstablishedByUserId = pilotChefId ?? 1,
                    VerifiedByUserId = pilotQualiteId ?? 1,
                    VerifiedAt = DateTime.UtcNow.AddDays(-7),
                    ValidatedByUserId = (int?)null,
                    ValidatedAt = (DateTime?)null,
                    EffectiveDate = (DateTime?)null,
                    ExpiryDate = (DateTime?)null
                });
            }

            if (recordDocumentId.HasValue)
            {
                var file = EnsureDemoDocumentFile(storageRoot, organizationId.Value, "DOC-ENR-001", "v1.0", "fiche_presence_v1_0.txt", "Fiche de presence - version 1.0");
                await connection.ExecuteAsync(insertDocumentVersionSql, new
                {
                    DocumentId = recordDocumentId.Value,
                    OrganizationId = organizationId.Value,
                    VersionNumber = "v1.0",
                    Status = "APPROUVE",
                    FileName = file.FileName,
                    OriginalFileName = file.OriginalFileName,
                    FilePath = file.RelativePath,
                    FileExtension = file.FileExtension,
                    MimeType = "text/plain",
                    FileSize = file.FileSize,
                    RevisionComment = "Modele approuve.",
                    EstablishedByUserId = pilotChefId ?? 1,
                    VerifiedByUserId = pilotChefId ?? 1,
                    VerifiedAt = DateTime.UtcNow.AddDays(-30),
                    ValidatedByUserId = pilotQualiteId ?? 1,
                    ValidatedAt = DateTime.UtcNow.AddDays(-29),
                    EffectiveDate = DateTime.UtcNow.AddDays(-29),
                    ExpiryDate = DateTime.UtcNow.AddMonths(12)
                });
            }

            if (formDocumentId.HasValue)
            {
                var file = EnsureDemoDocumentFile(storageRoot, organizationId.Value, "DOC-FORM-001", "v1.1", "formulaire_reclamation_v1_1.txt", "Formulaire de reclamation - version 1.1");
                await connection.ExecuteAsync(insertDocumentVersionSql, new
                {
                    DocumentId = formDocumentId.Value,
                    OrganizationId = organizationId.Value,
                    VersionNumber = "v1.1",
                    Status = "PERIME",
                    FileName = file.FileName,
                    OriginalFileName = file.OriginalFileName,
                    FilePath = file.RelativePath,
                    FileExtension = file.FileExtension,
                    MimeType = "text/plain",
                    FileSize = file.FileSize,
                    RevisionComment = "Version perimee en attente de remplacement.",
                    EstablishedByUserId = pilotSupportId ?? 1,
                    VerifiedByUserId = pilotQualiteId ?? 1,
                    VerifiedAt = DateTime.UtcNow.AddMonths(-10),
                    ValidatedByUserId = pilotQualiteId ?? 1,
                    ValidatedAt = DateTime.UtcNow.AddMonths(-10),
                    EffectiveDate = DateTime.UtcNow.AddMonths(-10),
                    ExpiryDate = DateTime.UtcNow.AddDays(-15)
                });
            }

            var documentIds = await connection.QueryAsync<int>(
                "SELECT Id FROM Documents WHERE OrganizationId = @OrganizationId",
                new { OrganizationId = organizationId.Value });

            foreach (var documentId in documentIds)
            {
                var currentVersionId = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT Id FROM DocumentVersions WHERE DocumentId = @DocumentId ORDER BY CreatedAt DESC, Id DESC LIMIT 1",
                    new { DocumentId = documentId });

                if (!currentVersionId.HasValue)
                {
                    continue;
                }

                await connection.ExecuteAsync(
                    "UPDATE DocumentVersions SET IsCurrent = CASE WHEN Id = @CurrentVersionId THEN TRUE ELSE FALSE END, UpdatedAt = NOW() WHERE DocumentId = @DocumentId",
                    new { DocumentId = documentId, CurrentVersionId = currentVersionId.Value });

                await connection.ExecuteAsync(
                    "UPDATE Documents SET CurrentVersionId = @CurrentVersionId, UpdatedAt = NOW() WHERE Id = @DocumentId",
                    new { DocumentId = documentId, CurrentVersionId = currentVersionId.Value });
            }

            const string insertNonConformitySql = @"
                INSERT INTO NonConformities
                    (OrganizationId, Code, Title, Description, Type, Severity, ProcessId, ProcedureId, DetectedDate, ResponsibleUserId, Status, CreatedAt)
                VALUES
                    (@OrganizationId, @Code, @Title, @Description, @Type, @Severity, @ProcessId, @ProcedureId, @DetectedDate, @ResponsibleUserId, @Status, NOW())
                ON CONFLICT (OrganizationId, Code) DO NOTHING;
            ";

            if (realisationId.HasValue)
            {
                await connection.ExecuteAsync(insertNonConformitySql, new
                {
                    OrganizationId = organizationId.Value,
                    Code = "NC-001",
                    Title = "Dossier inscription incomplet",
                    Description = "Des dossiers ont ete valides sans l'ensemble des pieces justificatives.",
                    Type = "INTERNE",
                    Severity = "MAJEURE",
                    ProcessId = realisationId.Value,
                    ProcedureId = procedure2Id,
                    DetectedDate = DateTime.UtcNow.AddDays(-6),
                    ResponsibleUserId = pilotChefId ?? pilotQualiteId ?? pilotSupportId ?? 1,
                    Status = "EN_COURS"
                });
            }

            if (supportId.HasValue)
            {
                await connection.ExecuteAsync(insertNonConformitySql, new
                {
                    OrganizationId = organizationId.Value,
                    Code = "NC-002",
                    Title = "Interruption critique du service informatique",
                    Description = "Indisponibilite de la plateforme qualite superieure a 4 heures.",
                    Type = "EXTERNE",
                    Severity = "CRITIQUE",
                    ProcessId = supportId.Value,
                    ProcedureId = procedure3Id,
                    DetectedDate = DateTime.UtcNow.AddDays(-10),
                    ResponsibleUserId = pilotSupportId ?? pilotQualiteId ?? pilotChefId ?? 1,
                    Status = "OUVERTE"
                });
            }

            const string insertCorrectiveActionSql = @"
                INSERT INTO CorrectiveActions
                    (OrganizationId, NonConformityId, Type, Title, Description, ResponsibleUserId, DueDate, Status, CompletionDate, CreatedAt)
                SELECT
                    @OrganizationId,
                    @NonConformityId,
                    @Type,
                    @Title,
                    @Description,
                    @ResponsibleUserId,
                    @DueDate,
                    @Status,
                    @CompletionDate,
                    NOW()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM CorrectiveActions
                    WHERE OrganizationId = @OrganizationId
                      AND NonConformityId = @NonConformityId
                      AND Title = @Title
                );
            ";

            var nc1Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM NonConformities WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "NC-001" });

            var nc2Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM NonConformities WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "NC-002" });

            if (nc1Id.HasValue)
            {
                await connection.ExecuteAsync(insertCorrectiveActionSql, new
                {
                    OrganizationId = organizationId.Value,
                    NonConformityId = nc1Id.Value,
                    Type = "CORRECTIVE",
                    Title = "AC-001 - Corriger l'absence de validation documentaire",
                    Description = "Ajouter un controle bloquant avant validation finale.",
                    ResponsibleUserId = pilotChefId ?? pilotQualiteId ?? 1,
                    DueDate = DateTime.UtcNow.AddDays(5),
                    CompletionDate = (DateTime?)null,
                    Status = "EN_COURS"
                });

                await connection.ExecuteAsync(insertCorrectiveActionSql, new
                {
                    OrganizationId = organizationId.Value,
                    NonConformityId = nc1Id.Value,
                    Type = "CURATIVE",
                    Title = "AC-002 - Reviser la procedure d'inscription",
                    Description = "Actualiser la procedure et communiquer la nouvelle version.",
                    ResponsibleUserId = pilotQualiteId ?? pilotChefId ?? 1,
                    DueDate = DateTime.UtcNow.AddDays(12),
                    CompletionDate = (DateTime?)null,
                    Status = "PLANIFIEE"
                });
            }

            if (nc2Id.HasValue)
            {
                await connection.ExecuteAsync(insertCorrectiveActionSql, new
                {
                    OrganizationId = organizationId.Value,
                    NonConformityId = nc2Id.Value,
                    Type = "CORRECTIVE",
                    Title = "AC-003 - Former le personnel sur le processus qualite",
                    Description = "Lancer une session de formation et faire signer les feuilles de presence.",
                    ResponsibleUserId = pilotSupportId ?? pilotQualiteId ?? 1,
                    DueDate = DateTime.UtcNow.AddDays(-2),
                    CompletionDate = (DateTime?)null,
                    Status = "EN_COURS"
                });

                await connection.ExecuteAsync(insertCorrectiveActionSql, new
                {
                    OrganizationId = organizationId.Value,
                    NonConformityId = nc2Id.Value,
                    Type = "RISQUE",
                    Title = "AC-004 - Mettre en place une verification mensuelle",
                    Description = "Instaurer un controle mensuel avec rapport de suivi.",
                    ResponsibleUserId = pilotQualiteId ?? pilotSupportId ?? 1,
                    DueDate = DateTime.UtcNow.AddDays(-10),
                    CompletionDate = DateTime.UtcNow.AddDays(-3),
                    Status = "REALISEE"
                });
            }

            const string insertIndicatorSql = @"
                INSERT INTO Indicators
                    (OrganizationId, ProcessId, Code, Name, Description, CalculationMethod, Unit, TargetValue, AlertThreshold, MeasurementFrequency, ResponsibleUserId, Status, CreatedAt)
                VALUES
                    (@OrganizationId, @ProcessId, @Code, @Name, @Description, @CalculationMethod, @Unit, @TargetValue, @AlertThreshold, @MeasurementFrequency, @ResponsibleUserId, @Status, NOW())
                ON CONFLICT (OrganizationId, Code) DO NOTHING;
            ";

            var indicatorProcessPilotage = pilotageId ?? realisationId ?? supportId;
            var indicatorProcessRealisation = realisationId ?? pilotageId ?? supportId;
            var indicatorProcessSupport = supportId ?? pilotageId ?? realisationId;

            if (indicatorProcessPilotage.HasValue && indicatorProcessRealisation.HasValue && indicatorProcessSupport.HasValue)
            {
                await connection.ExecuteAsync(insertIndicatorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = indicatorProcessPilotage.Value,
                    Code = "IND-001",
                    Name = "Taux de conformite documentaire",
                    Description = "Mesure le pourcentage de documents conformes lors des controles qualite.",
                    CalculationMethod = "(Documents conformes / Documents controles) * 100",
                    Unit = "%",
                    TargetValue = 95m,
                    AlertThreshold = 90m,
                    MeasurementFrequency = "MENSUEL",
                    ResponsibleUserId = pilotQualiteId ?? pilotChefId ?? 1,
                    Status = "ACTIF"
                });

                await connection.ExecuteAsync(insertIndicatorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = indicatorProcessRealisation.Value,
                    Code = "IND-002",
                    Name = "Delai moyen de traitement des non-conformites",
                    Description = "Indicateur de performance du traitement des non-conformites.",
                    CalculationMethod = "Score de respect des delais de traitement des NC",
                    Unit = "points",
                    TargetValue = 85m,
                    AlertThreshold = 80m,
                    MeasurementFrequency = "MENSUEL",
                    ResponsibleUserId = pilotChefId ?? pilotQualiteId ?? 1,
                    Status = "ACTIF"
                });

                await connection.ExecuteAsync(insertIndicatorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = indicatorProcessSupport.Value,
                    Code = "IND-003",
                    Name = "Taux de realisation des actions correctives",
                    Description = "Pourcentage d'actions correctives executees dans les delais.",
                    CalculationMethod = "(Actions realisees a temps / Actions planifiees) * 100",
                    Unit = "%",
                    TargetValue = 90m,
                    AlertThreshold = 85m,
                    MeasurementFrequency = "MENSUEL",
                    ResponsibleUserId = pilotSupportId ?? pilotQualiteId ?? 1,
                    Status = "ACTIF"
                });

                await connection.ExecuteAsync(insertIndicatorSql, new
                {
                    OrganizationId = organizationId.Value,
                    ProcessId = indicatorProcessPilotage.Value,
                    Code = "IND-004",
                    Name = "Taux de validation des procedures",
                    Description = "Suivi du taux de procedures validees conformement au planning.",
                    CalculationMethod = "(Procedures validees / Procedures prevues) * 100",
                    Unit = "%",
                    TargetValue = 92m,
                    AlertThreshold = 88m,
                    MeasurementFrequency = "TRIMESTRIEL",
                    ResponsibleUserId = pilotQualiteId ?? pilotChefId ?? 1,
                    Status = "ACTIF"
                });
            }

            const string insertIndicatorValueSql = @"
                INSERT INTO IndicatorValues
                    (OrganizationId, IndicatorId, PeriodLabel, MeasuredValue, Comment, MeasuredAt, EnteredByUserId, CreatedAt)
                SELECT
                    @OrganizationId,
                    i.Id,
                    @PeriodLabel,
                    @MeasuredValue,
                    @Comment,
                    @MeasuredAt,
                    @EnteredByUserId,
                    NOW()
                FROM Indicators i
                WHERE i.OrganizationId = @OrganizationId
                  AND i.Code = @IndicatorCode
                ON CONFLICT (IndicatorId, PeriodLabel) DO NOTHING;
            ";

            var indicatorValueActorId = pilotQualiteId ?? pilotChefId ?? pilotSupportId ?? 1;

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-001",
                PeriodLabel = "2025-11",
                MeasuredValue = 89m,
                Comment = "Lancement de campagne de mise a jour documentaire.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-5),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-001",
                PeriodLabel = "2025-12",
                MeasuredValue = 91m,
                Comment = "Amelioration suite aux actions de controle.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-4),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-001",
                PeriodLabel = "2026-01",
                MeasuredValue = 93m,
                Comment = "Consolidation des controles qualite.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-3),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-001",
                PeriodLabel = "2026-02",
                MeasuredValue = 96m,
                Comment = "Objectif depasse ce mois-ci.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-2),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-002",
                PeriodLabel = "2025-11",
                MeasuredValue = 78m,
                Comment = "Retards ponctuels sur le traitement des NC.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-5),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-002",
                PeriodLabel = "2025-12",
                MeasuredValue = 80m,
                Comment = "Stabilisation du delai moyen.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-4),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-002",
                PeriodLabel = "2026-01",
                MeasuredValue = 84m,
                Comment = "Amelioration progressive.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-3),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-002",
                PeriodLabel = "2026-02",
                MeasuredValue = 86m,
                Comment = "Objectif atteint ce mois-ci.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-2),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-003",
                PeriodLabel = "2025-11",
                MeasuredValue = 87m,
                Comment = "Suivi correct mais sous la cible.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-5),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-003",
                PeriodLabel = "2025-12",
                MeasuredValue = 85m,
                Comment = "Niveau seuil atteint.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-4),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-003",
                PeriodLabel = "2026-01",
                MeasuredValue = 83m,
                Comment = "Alerte sur baisse de performance.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-3),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-003",
                PeriodLabel = "2026-02",
                MeasuredValue = 78m,
                Comment = "Performance insuffisante, plan d'action requis.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-2),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-004",
                PeriodLabel = "2025-Q3",
                MeasuredValue = 89m,
                Comment = "Validation en progression.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-8),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-004",
                PeriodLabel = "2025-Q4",
                MeasuredValue = 91m,
                Comment = "Resultat proche de la cible.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-5),
                EnteredByUserId = indicatorValueActorId
            });

            await connection.ExecuteAsync(insertIndicatorValueSql, new
            {
                OrganizationId = organizationId.Value,
                IndicatorCode = "IND-004",
                PeriodLabel = "2026-Q1",
                MeasuredValue = 93m,
                Comment = "Cible depassee sur le trimestre.",
                MeasuredAt = DateTime.UtcNow.AddMonths(-2),
                EnteredByUserId = indicatorValueActorId
            });

            var indicator003Id = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Indicators WHERE OrganizationId = @OrganizationId AND Code = @Code LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "IND-003" });

            var indicator003LatestValueId = await connection.QueryFirstOrDefaultAsync<int?>(
                @"SELECT iv.Id
                  FROM IndicatorValues iv
                  INNER JOIN Indicators i ON i.Id = iv.IndicatorId
                  WHERE i.OrganizationId = @OrganizationId
                    AND i.Code = @Code
                  ORDER BY iv.MeasuredAt DESC, iv.Id DESC
                  LIMIT 1",
                new { OrganizationId = organizationId.Value, Code = "IND-003" });

            if (indicator003Id.HasValue && indicator003LatestValueId.HasValue)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO IndicatorAlerts
                        (OrganizationId, IndicatorId, IndicatorValueId, AlertType, Message, IsResolved, CreatedAt)
                      SELECT
                        @OrganizationId,
                        @IndicatorId,
                        @IndicatorValueId,
                        @AlertType,
                        @Message,
                        FALSE,
                        NOW()
                      WHERE NOT EXISTS (
                          SELECT 1
                          FROM IndicatorAlerts
                          WHERE OrganizationId = @OrganizationId
                            AND IndicatorId = @IndicatorId
                            AND IndicatorValueId = @IndicatorValueId
                            AND IsResolved = FALSE
                      )",
                    new
                    {
                        OrganizationId = organizationId.Value,
                        IndicatorId = indicator003Id.Value,
                        IndicatorValueId = indicator003LatestValueId.Value,
                        AlertType = "BELOW_THRESHOLD",
                        Message = "Le taux de realisation des actions correctives est sous le seuil d'alerte."
                    });
            }

            const string insertAlertRuleSql = @"
                INSERT INTO AlertRules
                    (OrganizationId, Code, Name, Description, EntityType, TriggerType, IsActive, ThresholdValue, CreatedAt)
                VALUES
                    (@OrganizationId, @Code, @Name, @Description, @EntityType, @TriggerType, @IsActive, @ThresholdValue, NOW())
                ON CONFLICT (OrganizationId, Code) DO NOTHING;
            ";

            await connection.ExecuteAsync(insertAlertRuleSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "NONCONFORMITY_CRITICAL",
                Name = "Alerte non-conformite critique",
                Description = "Alerte envoyee quand une non-conformite critique est ouverte.",
                EntityType = "NON_CONFORMITY",
                TriggerType = "ON_CRITICAL_OPEN",
                IsActive = true,
                ThresholdValue = (decimal?)null
            });

            await connection.ExecuteAsync(insertAlertRuleSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "CORRECTIVE_ACTION_OVERDUE",
                Name = "Alerte action corrective en retard",
                Description = "Alerte envoyee pour les actions correctives en retard.",
                EntityType = "CORRECTIVE_ACTION",
                TriggerType = "ON_OVERDUE",
                IsActive = true,
                ThresholdValue = (decimal?)null
            });

            await connection.ExecuteAsync(insertAlertRuleSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "DOCUMENT_EXPIRED",
                Name = "Alerte document perime",
                Description = "Alerte envoyee pour les documents perimes.",
                EntityType = "DOCUMENT",
                TriggerType = "ON_EXPIRED",
                IsActive = true,
                ThresholdValue = (decimal?)null
            });

            await connection.ExecuteAsync(insertAlertRuleSql, new
            {
                OrganizationId = organizationId.Value,
                Code = "INDICATOR_ALERT",
                Name = "Alerte indicateur KPI",
                Description = "Alerte envoyee quand un indicateur passe sous le seuil d'alerte.",
                EntityType = "INDICATOR",
                TriggerType = "ON_THRESHOLD_BREACH",
                IsActive = true,
                ThresholdValue = (decimal?)null
            });

            await connection.ExecuteAsync(@"
                INSERT INTO document_expiration_policies
                    (organization_id, alert_days_30, alert_days_7, alert_days_1, is_active, created_at)
                VALUES
                    (@OrganizationId, 30, 7, 1, TRUE, NOW())
                ON CONFLICT (organization_id) DO NOTHING;",
                new { OrganizationId = organizationId.Value });

            await connection.ExecuteAsync(@"
                INSERT INTO notification_rules
                    (organization_id, event_type, role_type, email_enabled, in_app_enabled, is_active, created_at)
                VALUES
                    (@OrganizationId, @EventType, @RoleType, TRUE, TRUE, TRUE, NOW())
                ON CONFLICT (organization_id, event_type, role_type) DO NOTHING;",
                new[]
                {
                    new { OrganizationId = organizationId.Value, EventType = "DocumentCreated", RoleType = "QualityManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentSubmitted", RoleType = "DepartmentManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentSubmitted", RoleType = "QualityManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentApproved", RoleType = "Employee" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentApproved", RoleType = "DepartmentManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentApproved", RoleType = "QualityManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentRejected", RoleType = "Employee" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentRejected", RoleType = "DepartmentManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentRejected", RoleType = "QualityManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentArchived", RoleType = "QualityManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentExpired", RoleType = "DepartmentManager" },
                    new { OrganizationId = organizationId.Value, EventType = "DocumentExpired", RoleType = "QualityManager" }
                });

            var adminOrgId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Users WHERE Email = @Email LIMIT 1",
                new { Email = "admin@demo.local" });

            var overdueActionId = await connection.QueryFirstOrDefaultAsync<int?>(
                @"SELECT Id
                  FROM CorrectiveActions
                  WHERE OrganizationId = @OrganizationId
                    AND DueDate < NOW()
                    AND Status NOT IN ('REALISEE', 'VERIFIEE')
                  ORDER BY DueDate ASC, Id DESC
                  LIMIT 1",
                new { OrganizationId = organizationId.Value });

            const string insertNotificationSql = @"
                INSERT INTO Notifications
                    (OrganizationId, UserId, Type, Category, Title, Message, Priority, IsRead, ReadAt, IsArchived, ReferenceType, ReferenceId, ActionUrl, CreatedAt)
                VALUES
                    (@OrganizationId, @UserId, @Type, @Category, @Title, @Message, @Priority, @IsRead, @ReadAt, @IsArchived, @ReferenceType, @ReferenceId, @ActionUrl, @CreatedAt);
            ";

            var seedNotifications = new List<SeedNotification>();

            if (pilotQualiteId.HasValue && procedureDocumentId.HasValue)
            {
                seedNotifications.Add(new SeedNotification
                {
                    OrganizationId = organizationId.Value,
                    UserId = pilotQualiteId.Value,
                    Type = "DOCUMENT_APPROVAL_REQUIRED",
                    Category = "INFO",
                    Title = "Document DOC-PROC-001 en attente de validation",
                    Message = "La version v2.0 du document Procedure d'inscription est en revision.",
                    Priority = "MEDIUM",
                    IsRead = false,
                    ReadAt = (DateTime?)null,
                    IsArchived = false,
                    ReferenceType = "DOCUMENT",
                    ReferenceId = procedureDocumentId.Value.ToString(),
                    ActionUrl = $"/documents/{procedureDocumentId.Value}/versions",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
            }

            if (adminOrgId.HasValue && nc2Id.HasValue)
            {
                seedNotifications.Add(new SeedNotification
                {
                    OrganizationId = organizationId.Value,
                    UserId = adminOrgId.Value,
                    Type = "NONCONFORMITY_CRITICAL",
                    Category = "ERROR",
                    Title = "Non-conformite critique NC-002 ouverte",
                    Message = "Interruption critique du service informatique - intervention immediate requise.",
                    Priority = "CRITICAL",
                    IsRead = false,
                    ReadAt = (DateTime?)null,
                    IsArchived = false,
                    ReferenceType = "NON_CONFORMITY",
                    ReferenceId = nc2Id.Value.ToString(),
                    ActionUrl = $"/non-conformities/{nc2Id.Value}",
                    CreatedAt = DateTime.UtcNow.AddHours(-20)
                });
            }

            if (pilotSupportId.HasValue && overdueActionId.HasValue && nc2Id.HasValue)
            {
                seedNotifications.Add(new SeedNotification
                {
                    OrganizationId = organizationId.Value,
                    UserId = pilotSupportId.Value,
                    Type = "CORRECTIVE_ACTION_OVERDUE",
                    Category = "WARNING",
                    Title = "Action corrective AC-004 en retard",
                    Message = "L'action corrective assignee est en retard. Veuillez mettre a jour le plan d'action.",
                    Priority = "HIGH",
                    IsRead = false,
                    ReadAt = (DateTime?)null,
                    IsArchived = false,
                    ReferenceType = "CORRECTIVE_ACTION",
                    ReferenceId = overdueActionId.Value.ToString(),
                    ActionUrl = $"/corrective-actions/{overdueActionId.Value}",
                    CreatedAt = DateTime.UtcNow.AddHours(-10)
                });
            }

            if (pilotQualiteId.HasValue)
            {
                seedNotifications.Add(new SeedNotification
                {
                    OrganizationId = organizationId.Value,
                    UserId = pilotQualiteId.Value,
                    Type = "INDICATOR_ALERT",
                    Category = "WARNING",
                    Title = "Indicateur KPI-003 sous seuil",
                    Message = "Le taux de conformite du processus Support est passe sous le seuil cible.",
                    Priority = "HIGH",
                    IsRead = false,
                    ReadAt = (DateTime?)null,
                    IsArchived = false,
                    ReferenceType = "INDICATOR",
                    ReferenceId = indicator003Id?.ToString() ?? "IND-003",
                    ActionUrl = indicator003Id.HasValue ? $"/indicators/{indicator003Id.Value}" : "/indicators",
                    CreatedAt = DateTime.UtcNow.AddHours(-6)
                });
            }

            if (pilotChefId.HasValue && recordDocumentId.HasValue)
            {
                seedNotifications.Add(new SeedNotification
                {
                    OrganizationId = organizationId.Value,
                    UserId = pilotChefId.Value,
                    Type = "DOCUMENT_NEW_VERSION",
                    Category = "SUCCESS",
                    Title = "Nouvelle version publiee pour DOC-ENR-001",
                    Message = "La version v1.0 de la fiche de presence est disponible.",
                    Priority = "LOW",
                    IsRead = false,
                    ReadAt = (DateTime?)null,
                    IsArchived = false,
                    ReferenceType = "DOCUMENT",
                    ReferenceId = recordDocumentId.Value.ToString(),
                    ActionUrl = $"/documents/{recordDocumentId.Value}",
                    CreatedAt = DateTime.UtcNow.AddHours(-4)
                });
            }

            if (seedNotifications.Count > 0)
            {
                const string existingNotificationSql = @"
                    SELECT COUNT(1)
                    FROM Notifications
                    WHERE UserId = @UserId
                      AND Type = @Type
                      AND COALESCE(ReferenceType, '') = COALESCE(@ReferenceType, '')
                      AND COALESCE(ReferenceId, '') = COALESCE(@ReferenceId, '')
                      AND CreatedAt >= (NOW() - INTERVAL '7 days')";

                var seedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var notification in seedNotifications)
                {
                    var seedKey = $"{notification.UserId}|{notification.Type}|{notification.ReferenceType ?? string.Empty}|{notification.ReferenceId ?? string.Empty}";
                    if (!seedKeys.Add(seedKey))
                    {
                        continue;
                    }

                    var exists = await connection.QuerySingleAsync<int>(existingNotificationSql, notification);
                    if (exists > 0)
                    {
                        continue;
                    }

                    try
                    {
                        await connection.ExecuteAsync(insertNotificationSql, notification);
                    }
                    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        logger.LogInformation(
                            "Notification de demo ignoree car deja existante pour user {UserId}, type {Type}, reference {ReferenceType}/{ReferenceId}.",
                            notification.UserId,
                            notification.Type,
                            notification.ReferenceType,
                            notification.ReferenceId);
                    }
                }
            }

            logger.LogInformation("Demo accounts ensured.");
        }

        private static string ResolveStorageRootPath(string? configuredRootPath)
        {
            var root = string.IsNullOrWhiteSpace(configuredRootPath) ? "StorageFiles" : configuredRootPath.Trim();
            return Path.IsPathRooted(root)
                ? root
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), root));
        }

        private static SeedFileInfo EnsureDemoDocumentFile(
            string storageRoot,
            int organizationId,
            string documentCode,
            string versionNumber,
            string fileName,
            string content)
        {
            var relativePath = Path.Combine(
                "documents",
                $"org-{organizationId}",
                documentCode.ToLowerInvariant(),
                versionNumber.ToLowerInvariant(),
                fileName).Replace('\\', '/');

            var absolutePath = Path.Combine(storageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(absolutePath) ?? storageRoot;
            Directory.CreateDirectory(directory);

            if (!File.Exists(absolutePath))
            {
                File.WriteAllText(absolutePath, content);
            }

            var info = new FileInfo(absolutePath);

            return new SeedFileInfo(
                FileName: fileName,
                OriginalFileName: fileName,
                RelativePath: relativePath,
                FileExtension: Path.GetExtension(fileName).ToLowerInvariant(),
                FileSize: info.Exists ? info.Length : 0);
        }

        private sealed class SeedNotification
        {
            public int OrganizationId { get; set; }
            public int UserId { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public bool IsRead { get; set; }
            public DateTime? ReadAt { get; set; }
            public bool IsArchived { get; set; }
            public string? ReferenceType { get; set; }
            public string? ReferenceId { get; set; }
            public string? ActionUrl { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed record DemoUser(
            string Email,
            string Password,
            string FirstName,
            string LastName,
            int? OrganizationId,
            string Role,
            string JobFunction);

        private sealed record SeedFileInfo(
            string FileName,
            string OriginalFileName,
            string RelativePath,
            string FileExtension,
            long FileSize);
    }
}

