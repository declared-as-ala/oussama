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
title Traiter une action corrective
skinparam sequenceMessageAlign center
skinparam responseMessageBelowArrow true
autonumber 1
autoactivate on

actor "Responsable Qualite" as RQ
actor "Responsable Action" as RA
boundary "Vue" as V
control "Controleur" as C
database "Modele" as M
control "Notification" as N

ref over RQ, N : Authentification

group 1. Creation action corrective
    RQ -> V : Saisir action corrective
    V -> C : creerActionCorrective(donnees)
    C -> M : verifierNonConformiteEtResponsable()
    
    alt Erreur verification
        M --> C : Erreur
        C --> V : Message erreur
        V --> RQ : Erreur affichee
    else OK
        M --> C : OK
        C -> M : enregistrerActionCorrective(Status=PLANIFIEE)
        C -> M : ajouterHistorique(CORRECTIVE_ACTION_CREATED)
        C -> N : notifierResponsable()
        N --> RA : Notification assignation
        C --> V : Action creee
        V --> RQ : Confirmation
    end
end

group 2. Gestion avancement
    alt Ajouter piece jointe
        RA -> V : Uploader fichier
        V -> C : ajouterPieceJointe(actionId, fichier)
        C -> M : enregistrerAttachment()
        C -> M : ajouterHistorique(ATTACHMENT_ADDED)
        C --> V : OK
        V --> RA : Fichier ajoute
    end
    
    alt Notifier completion
        RA -> V : Action terminee
        V -> C : notifierCompletion(actionId)
        C -> M : verifierResponsable()
        C -> M : ajouterHistorique(COMPLETION_NOTIFIED)
        C -> N : notifierRoles([ADMIN_ORG, RESPONSABLE_QUALITE])
        N --> RQ : A valider
        C --> V : OK
    end
end

group 3. Verification efficacite
    RQ -> V : Consulter action
    V -> C : consulterDetails(actionId)
    C -> M : chargerDetails()
    C --> V : Afficher
    
    RQ -> V : Changer statut REALISEE
    V -> C : mettreAJourStatut(Status=REALISEE)
    C -> M : verifierTransitionStatut()
    alt Transition invalide
        M --> C : Erreur
        C --> V : Erreur
    else OK
        M --> C : OK
        C -> M : enregistrerNouveauStatut(CompletionDate=NOW)
        C -> M : ajouterHistorique(STATUS_CHANGED)
        C -> N : notifierRoles()
        C --> V : OK
    end
    
    RQ -> V : Verifier efficacite (resultat + commentaire)
    V -> C : verifierEfficacite(actionId, EffectivenessVerified, Comment)
    C -> M : validerCommentaire()
    alt Commentaire vide
        M --> C : Erreur
        C --> V : Erreur
    else OK
        M --> C : OK
        C -> M : enregistrerVerification(Status=VERIFIEE si TRUE, sinon REALISEE)
        C -> M : ajouterHistorique(EFFECTIVENESS_VERIFIED)
        C -> M : mettreAJourEffectivenessComment()
        C --> V : OK
        V --> RQ : Action verifiee
    end
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
