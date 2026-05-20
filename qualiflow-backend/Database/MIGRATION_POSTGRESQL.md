# 🐘 MIGRATION POSTGRESQL - GUIDE COMPLET

## 📋 RÉSUMÉ DE LA MIGRATION

**De**: MySQL (Server=51.91.158.234:7004, Database=DocDb)
**Vers**: PostgreSQL (localhost:5432, Database=qualiosdb)

---

## ⚙️ CONFIGURATION

### appsettings.json
```json
{
  "ConnectionStrings": {
    // MySQL Configuration (Commented out - Using PostgreSQL instead)
    // "DefaultConnection": "Server=51.91.158.234;Port=7004;Database=DocDb;User=doc_user;Password=Stage55HHd;",
    
    // PostgreSQL Configuration
    "DefaultConnection": "Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyAtLeast32CharsLong!@@",
    "Issuer": "DocApi",
    "Audience": "DocApiUsers",
    "ExpirationInMinutes": 15
  }
}
```

### Program.cs - DbConnectionFactory (MUST UPDATE)

PostgreSQL utilise un connecteur différent. Modifier le DbConnectionFactory:

```csharp
// AVANT (MySQL):
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new MySqlConnection(connectionString);  // ❌ MySQL
    }
}

// APRÈS (PostgreSQL):
using Npgsql;  // ➕ ADD THIS USING

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new NpgsqlConnection(connectionString);  // ✅ PostgreSQL
    }
}
```

### Programme.cs - NuGet Packages

Remplacer MySql.Data avec Npgsql:

```bash
# AVANT: MySql.Data
dotnet remove package MySql.Data

# APRÈS: Npgsql (PostgreSQL)
dotnet add package Npgsql --version 8.0.1

# Garder Dapper (reste compatible)
# Dapper fonctionne avec PostgreSQL sans changement
```

### Package.json (appsettings)

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
},
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;"
},
"JwtSettings": {
  "SecretKey": "YourSuperSecretKeyAtLeast32CharsLong!@@",
  "Issuer": "DocApi",
  "Audience": "DocApiUsers",
  "ExpirationInMinutes": 15
},
"AllowedHosts": "*"
```

---

## 🔄 ÉTAPES DE MIGRATION

### 1️⃣ Installer PostgreSQL

**Windows**:
```bash
# Télécharger depuis https://www.postgresql.org/download/windows/
# Ou utiliser Chocolatey:
choco install postgresql

# Default: port 5432, user 'postgres', password 'postgres' (à remplacer par 'root')
```

**Mac**:
```bash
brew install postgresql@15
```

**Linux (Ubuntu)**:
```bash
sudo apt-get update
sudo apt-get install postgresql postgresql-contrib
```

### 2️⃣ Créer la base de données

```bash
# Connecter en tant que postgres
psql -U postgres

# Créer la database
CREATE DATABASE qualiosdb;

# Vérifier
\l

# Quitter psql
\q
```

### 3️⃣ Changer le mot de passe postgres (si besoin)

```bash
# Connecter en psql
psql -U postgres

# Changer le password
ALTER USER postgres WITH PASSWORD 'root';

# Quitter
\q
```

### 4️⃣ Modifier le code C#

**DbConnectionFactory.cs**:
```csharp
using System.Data;
using Npgsql;  // ✅ CHANGER MySQL en Npgsql

namespace DocApi.Infrastructure;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new NpgsqlConnection(connectionString);  // ✅ CHANGER
    }
}
```

### 5️⃣ Mettre à jour appsettings.json

Voir section ⚙️ CONFIGURATION ci-dessus

### 6️⃣ Exécuter le script SQL PostgreSQL

```bash
# Depuis le dossier Database
psql -U postgres -d qualiosdb -f CreateAuthModule_PostgreSQL.sql

# Ou manuellement:
psql -U postgres
\c qualiosdb
\i CreateAuthModule_PostgreSQL.sql
```

### 7️⃣ Vérifier la création des tables

```bash
psql -U postgres -d qualiosdb

# Afficher les tables
\dt

# Afficher les détails d'une table
\d "Users"
\d "Organizations"
\d "RefreshTokens"

# Quitter
\q
```

### 8️⃣ Rebuild et test

```bash
cd back
dotnet clean
dotnet restore
dotnet build
dotnet run

# Vérifier Swagger: http://localhost:5000/swagger
```

### 9️⃣ Tester login

```bash
# Via Swagger ou cURL:
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@demo.local",
    "password": "Admin@123"
  }'
```

---

## 📝 DIFFÉRENCES MYSQL vs POSTGRESQL

### Syntaxe SQL

| Feature | MySQL | PostgreSQL |
|---------|-------|-----------|
| Auto increment | SERIAL / AUTO_INCREMENT | SERIAL |
| Boolean | TINYINT(1) | BOOLEAN |
| String | VARCHAR(255) | VARCHAR(255) |
| Timestamp | DATETIME | TIMESTAMP |
| Enum | ENUM('A', 'B') | VARCHAR + CHECK |
| Null check | IS NULL | IS NULL |
| Insert duplicate | INSERT OR DUPLICATE KEY | ON CONFLICT DO NOTHING |
| Comments | `SELECT * \G` | Same |

### Paramètres Dapper (COMPATIBLE)

Bonnes news: **Dapper fonctionne avec PostgreSQL sans changement!**

```csharp
// ✅ Fonctionne avec MySQL ET PostgreSQL:

var user = await connection.QueryFirstOrDefaultAsync<User>(
    @"SELECT * FROM Users WHERE Email = @Email",
    new { Email = "admin@demo.local" }
);

var users = await connection.QueryAsync<User>(
    @"SELECT * FROM Users WHERE OrganizationId = @OrgId LIMIT @PageSize OFFSET @Offset",
    new { OrgId = 1, PageSize = 10, Offset = 0 }
);
```

### Connection String PostgreSQL

```
Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;
```

Alternativas:
```
Server=localhost;Port=5432;Database=qualiosdb;User Id=postgres;Password=root;
```

---

## 🔗 CHAÎNES DE CONNEXION ALTERNATIVES

### PostgreSQL lokal
```
Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;
```

### PostgreSQL cloud (Azure Database for PostgreSQL)
```
Host=myserver.postgres.database.azure.com;Port=5432;Database=qualiosdb;Username=postgres@myserver;Password=root;SslMode=Require;
```

### PostgreSQL cloud (AWS RDS)
```
Host=mydb.123456789.us-east-1.rds.amazonaws.com;Port=5432;Database=qualiosdb;Username=postgres;Password=root;
```

### PostgreSQL supabase (Firebase alternative)
```
Host=dbname.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=root;SslMode=Require;
```

---

## ⚠️ NOTES IMPORTANTES

### 1. Names SQL
PostgreSQL est **case-sensitive** pour les noms de colonnes non-quoted.

```sql
-- ❌ INCORRECT (PostgreSQL recherche "email" en minuscules)
SELECT email FROM users;

-- ✅ CORRECT (Double quotes pour case-sensitive)
SELECT "Email" FROM "Users";
```

Dapper gère cela automatiquement avec les propriétés C#.

### 2. Schema par défaut
PostgreSQL utilise "public" schema par défaut.

Si besoin de spécifier:
```csharp
// Dans connection string
Host=localhost;Port=5432;Database=qualiosdb;Username=postgres;Password=root;Search Path=public;
```

### 3. Transactions
PostgreSQL gère les transactions différemment:

```csharp
// Toujours spécifier le level d'isolation pour compatibilité
using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
{
    try
    {
        // Votre code
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

### 4. Date/Time
PostgreSQL stocke l'heure en UTC par défaut:

```csharp
// ✅ CORRECT
var now = DateTime.UtcNow;

// ❌ À ÉVITER
var now = DateTime.Now;  // Time zone issues
```

### 5. Backup/Restore

```bash
# Backup PostgreSQL
pg_dump -U postgres -d qualiosdb > backup.sql

# Restore PostgreSQL
psql -U postgres -d qualiosdb < backup.sql

# Backup avec compression
pg_dump -U postgres -d qualiosdb -Fc > backup.dump
pg_restore -U postgres -d qualiosdb backup.dump
```

---

## 🧪 VÉRIFICATION POST-MIGRATION

### Checklist
- [ ] DbConnectionFactory utilise NpgsqlConnection
- [ ] appsettings.json pointe vers qualiosdb
- [ ] PostgreSQL est démarré (port 5432)
- [ ] Database qualiosdb existe
- [ ] Tables sont créées (via CreateAuthModule_PostgreSQL.sql)
- [ ] Seed data inclus (6 users)
- [ ] Backend démarre sans erreur SQL
- [ ] Login works via Swagger
- [ ] Token is generated and stored

### Command line tests

```bash
# Vérifier la connexion
psql -U postgres -d qualiosdb -c "SELECT COUNT(*) FROM \"Users\";"

# Vérifier les users seedés
psql -U postgres -d qualiosdb -c "SELECT \"Email\", \"Role\" FROM \"Users\";"

# Vérifier les organizations
psql -U postgres -d qualiosdb -c "SELECT * FROM \"Organizations\";"
```

---

## 🚀 DÉMARRAGE APRÈS MIGRATION

```bash
# 1. Vérifier PostgreSQL is running
psql -U postgres -c "SELECT version();"

# 2. Rebuilt backend
cd back
dotnet clean
dotnet restore
dotnet build

# 3. Run backend
dotnet run

# 4. Test via Swagger
# http://localhost:5000/swagger

# 5. Test login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Admin@123"}'
```

---

## 📦 PACKAGES NÉCESSAIRES

### Avant (MySQL):
- MySql.Data
- Dapper

### Après (PostgreSQL):
```bash
dotnet add package Npgsql --version 8.0.1
# Dapper reste le même (compatible)
```

### Installation complète:
```bash
cd back
dotnet restore
# Cela va installer Npgsql automatiquement si Npgsql.csproj est présent
```

---

## 🆘 PROBLÈMES COURANTS

### "Unable to connect to any of the specified MySQL hosts"
**Cause**: Encore en MySQL mode
**Solution**: Vérifier DbConnectionFactory utilise Npgsql et appsettings corrects

### "column 'email' does not exist"
**Cause**: PostgreSQL case-sensitive sans quotes
**Solution**: Utiliser `"Email"` avec double quotes dans SQL

### "Server doesn't support ssl mode 'Require'"
**Cause**: PostgreSQL SSL non configuré
**Solution**: Ajouter `SslMode=Disable;` à la connection string (développement seulement)

### "role 'root' does not exist"
**Cause**: Mot de passe postgres pas changé
**Solution**: Changer password postgres via `ALTER USER postgres WITH PASSWORD 'root';`

### "database 'qualiosdb' does not exist"
**Cause**: Database pas créée
**Solution**: Créer via `CREATE DATABASE qualiosdb;` en psql

---

## 📚 RESSOURCES

- PostgreSQL Docs: https://www.postgresql.org/docs/
- Npgsql Docs: https://www.npgsql.org/
- Dapper PostgreSQL: https://github.com/DapperLib/Dapper
- Connection String Builder: https://www.connectionstrings.com/

---

## ✅ MIGRATION COMPLETE!

Tous les fichiers PostgreSQL sont prêts:
- ✅ CreateAuthModule_PostgreSQL.sql
- ✅ DbConnectionFactory.cs (ready to update)
- ✅ appsettings.json (configured)
- ✅ Ce guide complet

Prochaines étapes:
1. Installer PostgreSQL
2. Créer database qualiosdb
3. Modifier DbConnectionFactory.cs
4. Exécuter CreateAuthModule_PostgreSQL.sql
5. Tester login via API

🎉 Bonne migration!
