# DocApi Backend - Documentation detaillee

API REST .NET 8 pour QualiFlow (gestion qualite, documents, processus, procedures, non-conformites, actions correctives, indicateurs, notifications, utilisateurs, organisations).

## Sommaire

1. Vue d ensemble
2. Stack technique
3. Architecture du code
4. Modules metier et endpoints
5. Securite et authentification
6. Configuration
7. Demarrage local
8. Base de donnees et initialisation auto
9. Gestion des fichiers
10. Comptes de demo
11. Exemples API
12. Troubleshooting

## 1) Vue d ensemble

Le backend expose une API ASP.NET Core avec :

- authentification JWT + refresh tokens
- controle d acces par roles metier
- persistance PostgreSQL via Dapper
- creation automatique du schema + seed de donnees de demo
- endpoints pour tous les modules qualite
- Swagger en environnement Development

Port local par defaut : `http://localhost:5185` (voir `Properties/launchSettings.json`).

## 2) Stack technique

- .NET 8 (`net8.0`)
- ASP.NET Core Web API
- Dapper
- Npgsql (PostgreSQL)
- JWT Bearer Authentication
- BCrypt.Net-Next (hash mots de passe)
- Swashbuckle (Swagger/OpenAPI)
- PdfSharpCore (generation/traitement PDF)

Packages references : `DocApi.csproj`.

## 3) Architecture du code

Structure principale :

```text
back/
|-- Controllers/          # Endpoints HTTP (validation/autorisation/reponses)
|-- Services/             # Logique metier
|   `-- Interfaces/       # Contrats de services
|-- Repositories/         # Acces donnees SQL via Dapper
|   `-- Interfaces/       # Contrats de repositories
|-- Domain/Entities/      # Entites metier
|-- DTOs/                 # Contrats requete/reponse API
|-- Infrastructure/       # DB init, storage local, PDF, connexion DB
|-- Common/               # Constantes, roles, exceptions metier
|-- Database/             # Scripts SQL PostgreSQL par module
|-- StorageFiles/         # Fichiers stockes localement
|-- Program.cs            # Composition root (DI, auth, cors, middleware)
`-- appsettings.json      # Config principale
```

Pipeline applicatif (`Program.cs`) :

1. Enregistrement DI (repositories/services/infrastructure)
2. JWT auth + authorization
3. Swagger (Development)
4. CORS (`http://localhost:4200`, `http://localhost:3000`)
5. `DatabaseInitializer.InitializeAsync(...)` au startup
6. Mapping controllers

Note : `UseHttpsRedirection()` est actif seulement hors Development.

## 4) Modules metier et endpoints

Controllers exposes :

- `api/auth`
- `api/users`
- `api/organizations`
- `api/processes`
- `api/procedures`
- `api/documents`
- `api/nonconformities`
- `api/corrective-actions`
- `api/indicators`
- `api/notifications`
- `api/alert-rules`
- `api/dashboard/super-admin/*`
- `api/typedocument`
- `api/admin` (maintenance/admin techniques)

Resume fonctionnel :

- `auth` : register, login, refresh-token, logout, profile, photo, password reset
- `users` : CRUD users, activation/desactivation, changement de role
- `organizations` : CRUD orgs (super admin), profil org courant, logo
- `processes` : CRUD, map, stats, pilot, acteurs
- `procedures` : CRUD, stats, instructions imbriquees
- `documents` : CRUD, versions, upload/download, preview, stats
- `nonconformities` : CRUD, statut, stats
- `corrective-actions` : CRUD, statut, verification efficacite, historique
- `indicators` : CRUD, valeurs, chart, alerts, stats
- `notifications` : liste, unread count, mark read/all, archive, delete, stats
- `alert-rules` : CRUD + activation/desactivation des regles
- `dashboard` : endpoints consolides super admin (kpis/charts/alerts/activities)

## 5) Securite et authentification

Roles metier principaux (`Common/UserRoles.cs`) :

- `SUPER_ADMIN`
- `ADMIN_ORG`
- `RESPONSABLE_QUALITE`
- `CHEF_SERVICE`
- `UTILISATEUR`

JWT :

- `Issuer`, `Audience`, `SecretKey` dans `appsettings.json`
- expiration access token : 15 minutes (`JwtSettings:ExpirationInMinutes`)
- refresh token persiste en DB (validite 7 jours, gere par `AuthService`)

Bonnes pratiques recommandees en prod :

- remplacer la cle JWT par une vraie cle secrete forte
- ne jamais versionner de credentials reels
- limiter CORS aux domaines front de production

Point d attention :

- `TypeDocumentController` utilise encore `[Authorize(Roles = "Admin")]` pour ecriture.
- ce role ne correspond pas aux roles metier standards (`ADMIN_ORG`, etc.).

## 6) Configuration

Fichier cle : `appsettings.json`

Sections importantes :

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `DemoAccounts:EnableDemoAccounts`
- `Storage` (extensions autorisees, tailles max, chemins)

Exemple local :

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;"
}
```

## 7) Demarrage local

## Prerequis

- .NET SDK 8
- PostgreSQL 14+ (15/16 OK)

## Etapes

1. Creer la base PostgreSQL (si absente) :

```sql
CREATE DATABASE qualiosdb;
```

2. Restaurer et lancer l API :

```bash
cd back
dotnet restore
dotnet run
```

3. Ouvrir Swagger (Development) :

- `http://localhost:5185/swagger`

4. Tester un login, puis cliquer `Authorize` dans Swagger :

- valeur : `Bearer <access_token>`

## Build

```bash
cd back
dotnet build
```

Si `DocApi.dll` est verrouille par un process en cours, builder vers un dossier dedie :

```bash
dotnet build -o artifacts/build
```

## 8) Base de donnees et initialisation auto

`Infrastructure/DatabaseInitializer.cs` :

- cree les tables/index si absents
- applique des evolutions de schema defensives (`IF NOT EXISTS`)
- seed des organisations, users et donnees metier de demo
- seed des fichiers de demo dans `StorageFiles`

Le seed est active si :

- `DemoAccounts:EnableDemoAccounts = true`

Scripts SQL disponibles (complements / migration manuelle) dans `Database/` :

- `CreateAuthModule_PostgreSQL.sql`
- `CreateProcessModule_PostgreSQL.sql`
- `CreateProcedureModule_PostgreSQL.sql`
- `CreateDocumentModule_PostgreSQL.sql`
- `CreateNonConformityModule_PostgreSQL.sql`
- `CreateCorrectiveActionModule_PostgreSQL.sql`
- `CreateIndicatorModule_PostgreSQL.sql`
- `CreateNotificationModule_PostgreSQL.sql`
- `CreateSuperAdminDashboard_PostgreSQL.sql`
- `MIGRATION_POSTGRESQL.md`

## 9) Gestion des fichiers

Stockage local configure via `Storage` :

- root : `StorageFiles`
- documents : `StorageFiles/documents/...`
- logos orgs : `StorageFiles/organization-logos`
- photos profil : `StorageFiles/profile-photos`

Controles :

- extensions autorisees
- taille max document (20 MB)
- taille max logo (5 MB)
- taille max photo profil (3 MB)
- option `PdfHeaderEnabled`

## 10) Comptes de demo

Comptes seedes automatiquement (si option active) :

- `superadmin@demo.local` / `SuperAdmin@123`
- `admin@demo.local` / `Admin@123`
- `qualite@demo.local` / `Qualite@123`
- `chef@demo.local` / `Chef@123`
- `user@demo.local` / `User@123`
- `user@demo.local` / `User@123`
- `admin.nord@demo.local` / `AdminNord@123`
- `admin.sud@demo.local` / `AdminSud@123`

## 11) Exemples API

Login :

```bash
curl -X POST http://localhost:5185/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@demo.local",
    "organizationCode": "DEMO",
    "password": "Admin@123"
  }'
```

Refresh token :

```bash
curl -X POST http://localhost:5185/api/auth/refresh-token \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'
```

Recuperer profil courant :

```bash
curl http://localhost:5185/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## 12) Troubleshooting

Erreur DB connexion :

- verifier service PostgreSQL demarre
- verifier host/port/user/password/database dans `appsettings.json`

401 Unauthorized :

- token absent/expire
- mauvais format header (`Bearer <token>`)
- role insuffisant sur endpoint cible

Swagger sans endpoints :

- verifier `ASPNETCORE_ENVIRONMENT=Development`
- relancer l API

Build bloque par fichier utilise :

- arreter process `dotnet run` actif
- ou builder avec `-o artifacts/build`

Erreur CORS depuis le front :

- verifier origine front (`localhost:4200` ou `localhost:3000`)
- ajuster policy `AllowFrontend` dans `Program.cs`

## 13) Deploiement Render (Docker)

Le dossier `back/` contient maintenant :

- `Dockerfile` (build multi-stage .NET 8)
- `.dockerignore` (contexte Docker plus leger)

### Etapes Render

1. Creer un `Web Service` sur Render
2. Connecter ton repo Git
3. Choisir le dossier racine `back`
4. Laisser Render builder le `Dockerfile`
5. Deployer

### Variables d environnement recommandees

Configurer ces variables dans Render :

- `ConnectionStrings__DefaultConnection` = URL PostgreSQL Render
- `JwtSettings__SecretKey` = cle secrete longue et robuste
- `JwtSettings__Issuer` = `DocApi` (ou valeur metier)
- `JwtSettings__Audience` = `DocApiUsers` (ou valeur metier)
- `JwtSettings__ExpirationInMinutes` = `15` (ou besoin metier)
- `DemoAccounts__EnableDemoAccounts` = `false` en production

Pour CORS production, ajouter les origines front autorisees :

- `Cors__AllowedOrigins__0` = `https://ton-front-web.onrender.com`
- `Cors__AllowedOrigins__1` = `capacitor://localhost`
- `Cors__AllowedOrigins__2` = `ionic://localhost`

Optionnel (utile avec Vercel previews / sous-domaines dynamiques) :

- `Cors__AllowedOriginPatterns__0` = `https://*.vercel.app`
- `Cors__AllowedOriginPatterns__1` = `https://*.onrender.com`

### Stockage fichiers (important)

Le filesystem container est ephemere sur Render.
Si tu veux conserver documents/logos/photos, monte un disque persistant Render et configure :

- `Storage__RootPath` = `/var/data/StorageFiles`
- `Storage__OrganizationLogosPath` = `/var/data/StorageFiles/organization-logos`
- `Storage__ProfilePhotosPath` = `/var/data/StorageFiles/profile-photos`
