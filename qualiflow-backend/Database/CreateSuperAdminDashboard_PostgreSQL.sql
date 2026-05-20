-- SUPER ADMIN DASHBOARD & ORGANIZATION MANAGEMENT (PostgreSQL)
-- This script is idempotent and compatible with existing schema.

-- Useful indexes
CREATE INDEX IF NOT EXISTS idx_organizations_name ON Organizations(Name);
CREATE INDEX IF NOT EXISTS idx_organizations_code ON Organizations(Code);
CREATE INDEX IF NOT EXISTS idx_organizations_status ON Organizations(Status);
CREATE INDEX IF NOT EXISTS idx_organizations_type ON Organizations(Type);

CREATE INDEX IF NOT EXISTS idx_users_organizationid ON Users(OrganizationId);
CREATE INDEX IF NOT EXISTS idx_users_role ON Users(Role);
CREATE INDEX IF NOT EXISTS idx_users_createdat ON Users(CreatedAt);


-- Demo organizations
INSERT INTO Organizations (Name, Code, Description, Type, Address, Email, Phone, Status, CreatedAt)
VALUES
    ('Institut Demo Nord', 'INST-NORD', 'Institut de demonstration zone nord', 'INSTITUT', 'Rue du Nord', 'contact@inst-nord.demo', '+21670001001', 'ACTIF', NOW()),
    ('Centre Demo Sud', 'CENTRE-SUD', 'Centre de demonstration zone sud', 'CENTRE', 'Avenue du Sud', 'contact@centre-sud.demo', '+21670001002', 'ACTIF', NOW()),
    ('Entreprise Demo', 'ENT-DEMO', 'Entreprise partenaire de demonstration', 'ENTREPRISE', 'Parc technologique', 'contact@ent-demo.local', '+21670001003', 'SUSPENDUE', NOW())
ON CONFLICT (Code) DO NOTHING;

-- Demo local admins (using pre-generated BCrypt hashes)
INSERT INTO Users (OrganizationId, FirstName, LastName, Email, Username, PasswordHash, Role, Function, Department, IsActive, CreatedAt)
SELECT o.Id, 'Admin', 'Nord', 'admin.nord@demo.local', 'admin.nord@demo.local', '$2a$11$5eFLZUvV6V4O3p0XzH.Q9e4K.zQ6T8W9V5R2L1N0M3A2B5C8D1F', 'ADMIN_ORG', 'Administrateur', 'Direction', TRUE, NOW()
FROM Organizations o
WHERE o.Code = 'INST-NORD'
  AND NOT EXISTS (SELECT 1 FROM Users u WHERE u.Email = 'admin.nord@demo.local');

INSERT INTO Users (OrganizationId, FirstName, LastName, Email, Username, PasswordHash, Role, Function, Department, IsActive, CreatedAt)
SELECT o.Id, 'Admin', 'Sud', 'admin.sud@demo.local', 'admin.sud@demo.local', '$2a$11$5eFLZUvV6V4O3p0XzH.Q9e4K.zQ6T8W9V5R2L1N0M3A2B5C8D1F', 'ADMIN_ORG', 'Administrateur', 'Direction', TRUE, NOW()
FROM Organizations o
WHERE o.Code = 'CENTRE-SUD'
  AND NOT EXISTS (SELECT 1 FROM Users u WHERE u.Email = 'admin.sud@demo.local');
