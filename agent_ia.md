# 🤖 Fiche d'Orientation QualiFlow pour l'Agent IA (agent_ia.md)

Ce fichier sert de point d'entrée et de guide de référence rapide pour toute Intelligence Artificielle (ou développeur) travaillant sur le projet **QualiFlow**. Son but est de fournir une compréhension immédiate de l'architecture, de la structure des fichiers et des flux de données, permettant ainsi de **gagner du temps** et d'**économiser des tokens** lors des recherches.

---

## 📌 1. Présentation Globale du Projet

**QualiFlow** est un système complet de gestion de la qualité (QSE). Il permet de gérer :
- L'authentification multi-locataire (multi-org).
- Les processus et procédures de l'entreprise.
- Le cycle de vie des documents (création, téléversement, versioning, estampillage PDF/Word/Excel).
- Les non-conformités et actions correctives (assignation, suivi, validation).
- Les indicateurs (KPI) et règles d'alerte.
- Un chatbot d'assistance technique intégré avec IA (Groq / OpenRouter).
- Les notifications en temps réel (SignalR, push Firebase, mails SMTP, files RabbitMQ).

### L'écosystème est divisé en 3 parties principales :
1. **Backend API (`qualiflow-backend`)** : API REST bâtie sous .NET 8, utilisant PostgreSQL (via Dapper) et RabbitMQ.
2. **Frontend Web (`qualiflow-frontend`)** : Application web Angular 17 stylisée avec Angular Material.
3. **Application Mobile (`mobile`)** : Application hybride Ionic / Angular (compatible Android/iOS via Capacitor).

---

## 🛠️ 2. Stack Technique & Dépendances

### Backend (`qualiflow-backend`)
- **Runtime** : .NET 8.0 (`DocApi.csproj`)
- **Persistance** : PostgreSQL (via le driver `Npgsql`)
- **ORM / Accès Données** : Dapper (requêtes SQL brutes et performantes)
- **Authentification** : JWT (JSON Web Tokens) + Refresh Tokens persistés en base de données.
- **Sécurité** : Chiffrement des mots de passe avec BCrypt.
- **Manipulation Fichiers** : `PdfSharpCore` pour l'insertion de cartouches/tampons d'estampillage de documents.
- **Communication temps réel / Asynchrone** :
  - **SignalR** : Notifications in-app immédiates.
  - **RabbitMQ** : File d'attente pour le traitement asynchrone des notifications (optionnel).
  - **Firebase Cloud Messaging (FCM)** : Notifications push sur mobile.
  - **SMTP** : Envoi d'emails d'alerte.
- **IA** : Intégration d'OpenRouter / Groq pour le support chatbot technique.

### Frontend Web (`qualiflow-frontend`)
- **Framework** : Angular 17.3
- **Design & UI** : Angular Material 17.3, ApexCharts (pour les indicateurs et tableaux de bord).
- **Communication** : Requêtes HTTP vers le backend + connexion client SignalR pour le hub de notifications.

### Mobile (`mobile`)
- **Framework** : Ionic 8 / Angular 20
- **Hybride** : Capacitor 8.3 (Android / iOS)
- **Fonctionnalités natives** : Notifications push locales et distantes, détection clavier, barre d'état.

---

## 📁 3. Carte de la Structure des Fichiers

### 💻 Backend: `qualiflow-backend/`
- [Program.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Program.cs) : Fichier central d'initialisation de l'application (Configuration DI, middlewares, CORS, Auth JWT).
- **`Controllers/`** : Points d'entrée de l'API (autorisation par rôles).
  - [AuthController.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Controllers/AuthController.cs) : Inscription, connexion, profils, reset de mot de passe.
  - [DocumentsController.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Controllers/DocumentsController.cs) : Upload, download, métadonnées et historique des versions de documents.
  - [CorrectiveActionsController.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Controllers/CorrectiveActionsController.cs) : Cycle de vie des actions correctives.
- **`Services/`** : Logique métier de l'application.
  - [AuthService.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Services/AuthService.cs) : Gestion des tokens JWT, hachage, validation.
  - [DocumentService.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Services/DocumentService.cs) : Logique de publication et de versioning documentaire.
  - [ChatbotService.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Services/ChatbotService.cs) : Appel de l'API d'IA et formatage du contexte.
- **`Repositories/`** : Contient le code SQL Dapper pour interagir avec PostgreSQL.
- **`Infrastructure/`** :
  - [DatabaseInitializer.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Infrastructure/DatabaseInitializer.cs) : Crée les tables et insère les données de démo au démarrage de l'application.
  - [PdfHeaderStampService.cs](file:///d:/qualilflow/oussama/qualiflow-backend/Infrastructure/PdfHeaderStampService.cs) : Ajoute automatiquement un cartouche de validation (tampon d'organisation, date, version) sur les fichiers PDF.
- **`Database/`** : Scripts SQL d'initialisation par module (ex: `CreateAuthModule_PostgreSQL.sql`).
- **`StorageFiles/`** : Répertoire local de stockage des fichiers téléversés.

### 🌐 Frontend: `qualiflow-frontend/`
- **`src/app/core/`** : Services transversaux, interceptors d'authentification (injection du token Bearer), et guards de routes.
- **`src/app/features/`** : Modules métier contenant les composants Angular :
  - `auth/`, `processes/`, `procedures/`, `documents/`, `non-conformities/`, `corrective-actions/`, `indicators/`, `notifications/`, `chatbot/`, `super-admin/`.
- **`src/environments/`** : Fichiers de configuration de l'API (ex: `environment.ts` pointe par défaut sur `http://localhost:5185`).

### 📱 Application Mobile: `mobile/`
- **`src/app/core/`** : Services d'API et stockage local (gestion du token).
- **`src/app/features/`** : Pages Ionic pour chaque fonctionnalité correspondante au Web (adaptées au format mobile).
- **`src/environments/`** : Configuration d'adresse d'API pour le mobile (ex: `LOCAL_API_URL` pointant sur l'adresse IP locale de développement ou sur Docker).

---

## 🗄️ 4. Base de Données & Schéma Principal

Les tables PostgreSQL principales sont préfixées par des guillemets (sensibles à la casse en PostgreSQL) :
- `"Organizations"` : Gère les organisations clientes (multi-tenant). Code unique (ex: `DEMO`).
- `"Users"` : Utilisateurs associés à une organisation ou `SUPER_ADMIN` (sans organisation).
- `"RefreshTokens"` & `"PasswordResetTokens"` : Jetons de sécurité.
- `"Notifications"` : Suivi des messages d'information/alerte par utilisateur.
- `"AlertRules"` : Seuils pour les indicateurs (KPI) et déclenchement d'alertes automatiques.
- `"Processes"` & `"Procedures"` : Définition des processus qualité.
- `"Documents"` & `"DocumentVersions"` : Gestion documentaire et historique de fichiers.
- `"NonConformities"` : Déclaration d'anomalies.
- `"CorrectiveActions"` : Actions planifiées pour corriger les non-conformités.

---

## 🔑 5. Comptes de Test & Démo (Seeded)

Si `DemoAccounts:EnableDemoAccounts` est activé dans `appsettings.json`, les comptes suivants sont créés automatiquement en base :

| Adresse Email | Mot de passe | Rôle QualiFlow | Organisation |
| :--- | :--- | :--- | :--- |
| **`superadmin@demo.local`** | `SuperAdmin@123` | `SUPER_ADMIN` | *Aucune* |
| **`admin@demo.local`** | `Admin@123` | `ADMIN_ORG` | `DEMO` |
| **`qualite@demo.local`** | `Qualite@123` | `RESPONSABLE_QUALITE` | `DEMO` |
| **`chef@demo.local`** | `Chef@123` | `CHEF_SERVICE` | `DEMO` |
| **`user@demo.local`** | `User@123` | `UTILISATEUR` | `DEMO` |

---

## ⚙️ 6. Commandes Utiles de Développement

### Lancer le projet complet via Docker Compose :
```bash
docker-compose up --build -d
```
*Le frontend est alors accessible sur le port `8080` (`http://localhost:8080`).*

### Lancer le Backend localement (.NET) :
```bash
cd qualiflow-backend
dotnet restore
dotnet run
```
*L'API tourne sur `http://localhost:5185`. Le Swagger de développement est disponible sur `http://localhost:5185/swagger`.*

### Lancer le Frontend localement (Angular) :
```bash
cd qualiflow-frontend
npm install
npm run start
```
*L'application web tourne sur `http://localhost:4200`.*

### Lancer le Mobile localement (Ionic) :
```bash
cd mobile
npm install
npm run start
```
*L'application tourne en mode de test web sur `http://localhost:8100`.*

---

## 🧠 7. Instructions pour les Futures Requêtes de l'Agent IA

Pour garder les sessions rapides et peu gourmandes en tokens, suivez ces règles :
1. **Consultez toujours ce fichier** pour localiser le service ou contrôleur à éditer.
2. **Utilisez `grep_search`** avec des requêtes précises plutôt que de lire des répertoires entiers.
3. **Évitez de lire des fichiers volumineux** (comme `DocumentService.cs` ou `DatabaseInitializer.cs` qui font plus de 1000 lignes) en entier. Lisez des blocs ciblés grâce aux paramètres `StartLine` et `EndLine` de l'outil `view_file`.
4. Si vous devez modifier une requête SQL, cherchez le Repository correspondant dans `qualiflow-backend/Repositories/`.
