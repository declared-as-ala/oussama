# Diagrammes de sequence academiques MVC - QualiFlow

Ces diagrammes sont prepares pour le memoire avec une presentation proche du modele academique classique: Acteur, Vue, Controleur et Modele. Les details techniques internes du backend sont regroupes dans le participant "Modele" afin de garder des figures lisibles.

## 1. Consulter les processus

```plantuml
@startuml
title Consulter les processus
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Administrateur" as A
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M

ref over A, M : Authentification

A -> V : Demander liste des processus
V -> C : recupererProcessus()
C -> M : chargerProcessus()
M --> C : listeProcessus
C --> V : mettreAJourInterface(listeProcessus)
V --> A : Afficher processus

alt Selection d'un processus
    A -> V : Selectionner processus
    V -> C : demanderDetails(processusId)
    C -> M : chargerDetailsProcessus(processusId)
    M --> C : detailsProcessus
    C --> V : afficherDetails(detailsProcessus)
    V --> A : Afficher details du processus
end

@enduml
```

## 2. Creer une organisation

```plantuml
@startuml
title Creer une organisation
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Super Administrateur" as SA
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M

ref over SA, M : Authentification

SA -> V : Saisir informations organisation
V -> C : creerOrganisation(donnees)
C -> M : verifierUnicite(nom, code)
M --> C : resultatVerification

alt Donnees valides
    C -> M : enregistrerOrganisation(donnees)
    M --> C : organisationCreee
    opt Premier administrateur fourni
        C -> M : creerCompteAdminOrganisation()
        M --> C : administrateurCree
    end
    C --> V : confirmerCreation(organisation)
    V --> SA : Afficher confirmation
else Donnees invalides
    C --> V : retournerErreurValidation()
    V --> SA : Afficher message d'erreur
end

@enduml
```

## 3. Creer un processus et affecter les acteurs

```plantuml
@startuml
title Creer un processus et affecter les acteurs
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Responsable Qualite" as RQ
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M

ref over RQ, M : Authentification

RQ -> V : Saisir processus et pilote
V -> C : creerProcessus(donnees)
C -> M : verifierCodeEtPilote(donnees)
M --> C : verificationOK
C -> M : enregistrerProcessus(donnees)
M --> C : processusCree
C --> V : afficherProcessus(processus)
V --> RQ : Confirmation creation

alt Affectation des acteurs
    RQ -> V : Selectionner acteurs
    V -> C : affecterActeurs(processusId, acteurs)
    C -> M : verifierActeurs(acteurs)
    M --> C : acteursValides
    C -> M : enregistrerAffectations(processusId, acteurs)
    M --> C : affectationsEnregistrees
    C --> V : mettreAJourListeActeurs()
    V --> RQ : Afficher acteurs affectes
end

@enduml
```

## 4. Gerer une procedure

```plantuml
@startuml
title Gerer une procedure
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Responsable Qualite" as RQ
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M

ref over RQ, M : Authentification

RQ -> V : Saisir nouvelle procedure
V -> C : creerProcedure(donnees)
C -> M : verifierProcessusRattache(processusId)
M --> C : processusValide
C -> M : enregistrerProcedure(donnees)
M --> C : procedureCreee
C --> V : afficherProcedure(procedure)
V --> RQ : Confirmation creation

alt Ajout d'une instruction
    RQ -> V : Saisir instruction
    V -> C : ajouterInstruction(procedureId, instruction)
    C -> M : verifierCodeInstruction()
    M --> C : codeValide
    C -> M : enregistrerInstruction()
    M --> C : instructionCreee
    C --> V : afficherInstruction(instruction)
    V --> RQ : Instruction ajoutee
end

@enduml
```

## 5. Gerer le cycle de vie documentaire

```plantuml
@startuml
title Gerer le cycle de vie documentaire
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Utilisateur autorise" as U
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M
collections "Stockage" as S

ref over U, S : Authentification

U -> V : Creer fiche document
V -> C : creerDocument(donnees)
C -> M : verifierRattachementsEtCode()
M --> C : verificationOK
C -> M : enregistrerDocument(donnees)
M --> C : documentCree
C --> V : afficherDocument(document)
V --> U : Document cree

alt Ajout d'une version
    U -> V : Selectionner fichier et version
    V -> C : uploaderVersion(documentId, fichier)
    C -> M : verifierVersion(documentId)
    M --> C : versionValide
    C -> S : stockerFichier(fichier)
    S --> C : cheminFichier
    C -> M : enregistrerVersion(cheminFichier)
    M --> C : versionCreee
    C --> V : afficherVersion(version)
    V --> U : Version ajoutee
end

alt Validation ou publication
    U -> V : Changer statut version
    V -> C : validerVersion(versionId, statut)
    C -> M : verifierDroitsValidation()
    M --> C : droitsValides
    C -> M : mettreAJourStatutVersion(statut)
    M --> C : statutMisAJour
    C --> V : afficherStatut(statut)
    V --> U : Statut mis a jour
end

@enduml
```

## 6. Declarer une non-conformite

```plantuml
@startuml
title Declarer une non-conformite
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Utilisateur Qualite" as U
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M
control "Notification" as N

ref over U, N : Authentification

U -> V : Saisir non-conformite
V -> C : declarerNonConformite(donnees)
C -> M : verifierProcessusProcedureResponsable()
M --> C : verificationOK
C -> M : enregistrerNonConformite(donnees)
M --> C : nonConformiteCreee
C -> N : notifierResponsable()
N --> C : notificationPreparee

alt Non-conformite critique
    C -> N : envoyerAlertePrioritaire()
    N --> C : alerteEnvoyee
end

C --> V : afficherNonConformite(nonConformite)
V --> U : Confirmation declaration

@enduml
```

## 7. Traiter une action corrective

```plantuml
@startuml
title **Traiter une action corrective**

' Style Premium & Compact
skinparam RoundCorner 6
skinparam BoxPadding 10
skinparam ParticipantPadding 10
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true

' Slate & Teal Theme
!define PRIMARY #1E293B
!define ACCENT #0EA5E9

skinparam ActorBackgroundColor #FFFFFF
skinparam ActorBorderColor PRIMARY
skinparam ActorFontColor PRIMARY
skinparam ActorFontWeight bold

skinparam ParticipantBackgroundColor #FFFFFF
skinparam ParticipantBorderColor PRIMARY
skinparam ParticipantFontColor PRIMARY
skinparam ParticipantFontWeight bold

skinparam DatabaseBackgroundColor #FFFFFF
skinparam DatabaseBorderColor PRIMARY
skinparam DatabaseFontColor PRIMARY

skinparam ArrowColor ACCENT
skinparam ArrowFontColor PRIMARY
skinparam ArrowFontWeight bold

skinparam SequenceLifeLineBackgroundColor #F8FAFC
skinparam SequenceLifeLineBorderColor ACCENT

skinparam GroupHeaderFontColor PRIMARY
skinparam GroupHeaderFontWeight bold
skinparam GroupBorderColor PRIMARY
skinparam GroupBackgroundColor #E2E8F0
skinparam GroupBodyBackgroundColor #FFFFFF

autonumber 1
autoactivate on

actor "**Responsable Qualité**\n*(RQ / Admin)*" as RQ
actor "**Responsable Action**\n*(Acteur)*" as RA
boundary "**Vue**" as V
control "**Contrôleur**" as C
control "**Service**" as S
database "**Base de données**" as M
control "**Notification**" as N

group **1. Création de l'action**
    RQ -> V : **Créer l'action corrective**
    V -> C : **Envoyer les données de l'action**
    C -> S : **Lancer la création de l'action**
    S -> M : **Vérifier et enregistrer l'action**
    M --> S : **Confirmation d'enregistrement (Id)**
    S -> N : **Déclencher la notification**
    N --> RA : **Alerte : Nouvelle action assignée**
    S --> C : **Détails de l'action créée**
    C --> V : **Confirmation de création**
    V --> RQ : **Succès de la création affiché**
end

group **2. Suivi de l'avancement (par l'Acteur)**
    alt **Ajouter une pièce jointe**
        RA -> V : **Ajouter un fichier**
        V -> C : **Uploader le document**
        C -> S : **Lancer l'ajout de la pièce jointe**
        S -> M : **Enregistrer le fichier & Historique**
        M --> S : **Confirmation d'enregistrement**
        S --> C : **Détails du fichier enregistré**
        C --> V : **Succès de l'upload**
    else **Signaler la fin des tâches**
        RA -> V : **Déclarer l'action comme terminée**
        V -> C : **Signaler la fin des travaux**
        C -> S : **Traiter la complétion**
        S -> M : **Enregistrer l'état & Historique**
        M --> S : **Confirmation**
        S -> N : **Notifier les validateurs**
        N --> RQ : **Alerte : Action à valider**
        S --> C : **Détails de l'action mise à jour**
        C --> V : **Confirmation d'envoi**
    end
end

group **3. Vérification de l'efficacité (par le RQ)**
    RQ -> V : **Consulter l'action à valider**
    V -> C : **Demander les détails de l'action**
    C -> S : **Lancer le chargement**
    S -> M : **Récupérer l'action, l'historique & PJ**
    M --> S : **Données complètes**
    S --> C : **Détails structurés**
    C --> V : **Afficher les détails de l'action**
    
    RQ -> V : **Évaluer l'efficacité (Résultat + Commentaire)**
    V -> C : **Envoyer les données d'évaluation**
    C -> S : **Vérifier l'efficacité**
    S -> M : **Enregistrer le statut final & Historique**
    M --> S : **Statut enregistré**
    S --> C : **Données de l'action clôturée**
    C --> V : **Confirmation de clôture**
    V --> RQ : **Action close et validée**
end

@enduml
```

## 8. Suivre les indicateurs et declencher les alertes

```plantuml
@startuml
title Suivre les indicateurs et declencher les alertes
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Responsable KPI" as RK
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M
control "Notification" as N

ref over RK, N : Authentification

RK -> V : Saisir valeur mesuree
V -> C : enregistrerValeur(indicateurId, valeur)
C -> M : chargerIndicateur(indicateurId)
M --> C : indicateur
C -> M : enregistrerValeurMesuree(valeur)
M --> C : valeurEnregistree
C -> C : comparerAvecCibleEtSeuil()

alt Seuil depasse
    C -> M : creerAlerteIndicateur()
    M --> C : alerteCreee
    C -> N : notifierResponsables()
    N --> C : notificationEnvoyee
    C --> V : afficherResultatAvecAlerte()
else Valeur conforme
    C -> M : cloturerAlertesOuvertes()
    M --> C : alertesCloturees
    C --> V : afficherResultatConforme()
end

V --> RK : Afficher etat indicateur

@enduml
```

## 9. Consulter le tableau de bord

```plantuml
@startuml
title Consulter le tableau de bord
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Administrateur" as A
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M

ref over A, M : Authentification

A -> V : Ouvrir tableau de bord
V -> C : demanderTableauDeBord(filtres)
C -> M : chargerKpis(filtres)
M --> C : kpis
C -> M : chargerGraphiques(filtres)
M --> C : donneesGraphiques
C -> M : chargerAlertesEtActivites(filtres)
M --> C : alertesEtActivites
C --> V : construireDashboard(donnees)
V --> A : Afficher statistiques et alertes

@enduml
```

## 10. Utiliser l'assistant chatbot

```plantuml
@startuml
title Utiliser l'assistant chatbot
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Utilisateur" as U
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M
control "Service IA" as IA

ref over U, IA : Authentification

U -> V : Poser une question
V -> C : envoyerQuestion(message)
C -> M : chargerHistoriqueConversation()
M --> C : historique
C -> C : preparerContexte()
C -> IA : demanderReponse(contexte)
IA --> C : reponseGeneree
C -> M : enregistrerConversation()
M --> C : conversationMiseAJour
C --> V : afficherReponse(reponse)
V --> U : Reponse affichee

@enduml
```
