# QualiFlow Frontend - Documentation detaillee

Application frontend Angular 17 pour la plateforme QualiFlow.

## Sommaire

1. Vue d ensemble
2. Stack technique
3. Arborescence du front
4. Architecture applicative
5. Navigation et gestion des acces
6. Communication avec l API
7. Installation et demarrage
8. Scripts npm
9. Comptes de demo
10. Build et tests
11. Troubleshooting
12. Notes techniques

## 1) Vue d ensemble

Le frontend couvre les modules metier suivants :

- authentification (login/register/forgot-password)
- dashboard utilisateur
- dashboard super-admin
- gestion organisations (super admin)
- processus
- procedures
- documents et versions
- non-conformites
- actions correctives
- indicateurs KPI
- notifications
- utilisateurs
- profil et parametres

## 2) Stack technique

- Angular `17.3.x`
- Standalone Components
- Angular Router (lazy loading via `loadComponent`)
- Angular Material + CDK
- RxJS
- SCSS
- Interceptors HTTP fonctionnels

## 3) Arborescence du front

```text
front/
|-- src/
|   |-- app/
|   |   |-- core/            # Services transverses, guards, interceptors
|   |   |-- features/        # Pages et modules metier
|   |   |-- layout/          # Header, sidebar, main layout
|   |   `-- shared/          # Composants/reutilisables (loading, dialogs, 404...)
|   |-- environments/        # Config dev/prod (apiUrl, flags)
|   |-- assets/              # Images et ressources statiques
|   `-- styles.scss          # Styles globaux
|-- angular.json             # Config build/serve/test Angular CLI
|-- package.json             # Scripts et dependances npm
`-- db.json                  # Mock API json-server (legacy/dev local)
```

Modules detectes dans `src/app/features` :

- `auth`
- `dashboard`
- `super-admin`
- `processes`
- `procedures`
- `documents`
- `non-conformities`
- `corrective-actions`
- `indicators`
- `notifications`
- `users`
- `organizations`
- `profile`
- `settings`
- `processus` (legacy)

## 4) Architecture applicative

Point d entree : `src/main.ts`

- bootstrap avec `bootstrapApplication`
- enregistrement du router
- activation animations Angular
- branchement des interceptors HTTP :
  - `authInterceptor`
  - `errorInterceptor`
  - `loadingInterceptor`

Service API central : `core/services/api.service.ts`

- construit toutes les URLs a partir de `${environment.apiUrl}/api`
- normalise les endpoints
- gere les query params pour les requetes GET

## 5) Navigation et gestion des acces

Routing central : `src/app/app-routing.module.ts`

- routes publiques :
  - `/login`
  - `/register`
  - `/forgot-password`
- zone privee sous le layout principal (`''`) avec enfants
- routes de fallback :
  - `/forbidden`
  - `/404`
  - `** -> /404`

Guards :

- `AuthGuard` :
  - bloque les routes privees sans token
  - applique les roles (`roles` ou `requiredRoles` dans `data`)
- `GuestGuard` :
  - empeche l acces login/register si utilisateur deja connecte
- `RoleGuard` :
  - verifie les roles et redirige vers `/forbidden`

## 6) Communication avec l API

Environnement dev :

```ts
apiUrl: 'http://localhost:5185'
```

Environnement prod :

```ts
apiUrl: 'https://your-api-url.com'
```

Interceptors :

- `authInterceptor` :
  - ajoute `Authorization: Bearer <token>` sur les appels proteges
  - exclut les endpoints publics d auth
- `errorInterceptor` :
  - centralise les messages d erreur
  - force le logout en cas de `401`
- `loadingInterceptor` :
  - affiche/masque le loader global pendant les requetes HTTP

Gestion session (AuthService) :

- stocke `accessToken` et `refreshToken` dans `localStorage`
- conserve `currentUser`
- gere profil courant (`/api/auth/me`)
- gere changement de langue/dir (`fr/en/ar`, rtl pour `ar`)

## 7) Installation et demarrage

Prerequis :

- Node.js 18+
- npm 9+
- backend API lance sur `http://localhost:5185`

Installation :

```bash
cd front
npm install
```

Demarrage (mode developpement) :

```bash
npm start
```

Application disponible sur :

- `http://localhost:4200`

## 8) Scripts npm

Depuis `front/package.json` :

- `npm start` : lance Angular dev server
- `npm run build` : build production
- `npm run watch` : build dev en watch
- `npm test` : tests unitaires (Karma)
- `npm run api` : lance `json-server` sur `http://localhost:3001` (mock legacy)
- `npm run dev` : lance `json-server` + Angular en parallele (mode legacy)

Important :

- l application est configuree par defaut pour le vrai backend (`apiUrl = http://localhost:5185`)
- les scripts `api/dev` sont surtout utiles pour scenarios mock anciens

## 9) Comptes de demo

Le login expose des boutons de comptes demo.

Comptes backend seedes (recommandes) :

- `superadmin@demo.local` / `SuperAdmin@123`
- `admin@demo.local` / `Admin@123`
- `qualite@demo.local` / `Qualite@123`
- `chef@demo.local` / `Chef@123`
- `user@demo.local` / `User@123`
- `user@demo.local` / `User@123`

Note :

- une entree UI `admin@test.com / admin@123` apparait dans la liste demo du composant login et peut ne pas exister cote backend seed.

## 10) Build et tests

Build production :

```bash
npm run build
```

Sortie :

- `dist/angular-professional-template`

Verification TypeScript (sans emission) :

```bash
npx tsc -p tsconfig.app.json --noEmit
```

Tests :

```bash
npm test
```

## 11) Troubleshooting

Front affiche "Impossible de joindre l API" :

- verifier que le backend tourne sur `http://localhost:5185`
- verifier `src/environments/environment.ts`
- verifier policy CORS du backend

Redirection vers `/login` en boucle :

- token absent/invalide dans `localStorage`
- endpoint `/api/auth/me` en erreur (401)

Erreur `403` sur une page :

- role utilisateur insuffisant pour la route (voir `roles`/`requiredRoles`)

Erreur build `spawn EPERM` (environnement Windows/sandbox) :

- relancer terminal/IDE en mode admin
- fermer process Node/Angular residuels
- nettoyer `node_modules` puis `npm install`

## 12) Notes techniques

- La route legacy `/processus` redirige vers `/processes`.
- Le dossier `features/processus` et certains services `core/services/*` correspondent a d anciens flux mock/in-memory.
- Pour les nouveaux developpements, privilegier les modules sous `features/*` relies a `ApiService`.
