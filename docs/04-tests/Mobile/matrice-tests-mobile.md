# Matrice de tests API Mobile SLI v1.3 strict

Cette matrice couvre le contrat mobile final prévu en `schemaVersion` `1.3` strict pour le choix camion et les kilométrages.

Cette mise à jour est documentaire. Elle ne signifie pas que le code API, les scripts SQL, les fichiers de tests automatisés, les payloads JSON ou l'application mobile MAUI sont déjà modifiés.

## Contrat 1.3 strict

```text
schemaVersion doit être exactement "1.3".
schemaVersion "1.2" doit être refusé.
Toute autre schemaVersion doit être refusée.
trajet obligatoire.
trajet.camion obligatoire.
trajet.camion.idCamion obligatoire.
trajet.kilometrageDepart obligatoire.
trajet.kilometrageArrivee obligatoire.
trajet.dateDepartMobile obligatoire.
trajet.dateArriveeMobile obligatoire.
kilometrageDepart >= 0.
kilometrageArrivee >= 0.
kilometrageArrivee >= kilometrageDepart.
dateArriveeMobile >= dateDepartMobile.
```

Les scénarios `MOB-API-001` à `MOB-API-018` restent présents. Les payloads valides associés à ces scénarios devront être prévus en `schemaVersion` `1.3` avec un bloc `trajet` valide, sauf pour les scénarios qui testent explicitement `schemaVersion`.

Aucun payload JSON n'est modifié dans ce lot documentaire.

## Scénarios

| ID | Scenario | Fichier | Type | HTTP attendu | Statut attendu | Code attendu |
|---|---|---|---|---:|---|---|
| MOB-API-001 | Synchronisation valide en 1.3 avec trajet valide | sync-valide.json | Valide | 200 | SUCCESS | |
| MOB-API-002 | Doublon technique idSynchronisation | sync-doublon.json | Conflit | 409 | CONFLICT | SYNCHRONISATION_ALREADY_EXISTS |
| MOB-API-003 | Double envoi metier date + tournee | sync-double-envoi-tournee.json | Conflit | 409 | CONFLICT | TOURNEE_ALREADY_SENT |
| MOB-API-004 | Quantite livree negative | sync-quantite-negative.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-005 | NON_FAIT sans commentaire | sync-non-fait-sans-commentaire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-006 | ANOMALIE sans commentaire | sync-anomalie-sans-commentaire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-007 | Ligne validee sans heureValidation | sync-validee-sans-heure.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-008 | estValidee false dans envoi final | sync-est-validee-false.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-009 | Statut A_FAIRE dans envoi final | sync-a-faire.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-010 | idLigneSource duplique | sync-idligne-duplique.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-011 | codeArticle duplique | sync-code-article-duplique.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-012 | schemaVersion invalide | sync-schema-version-invalide.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-013 | quantites vide | sync-quantites-vide.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-014 | quantiteLivreePrevue negative | sync-quantite-prevue-negative.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-015 | quantiteLivreePrevue zero acceptee en 1.3 avec trajet valide | sync-prevu-zero.json | Valide | 200 | SUCCESS | |
| MOB-API-016 | ROLLS_VIDES livre accepte en 1.3 avec trajet valide | sync-valide-rolls-vides.json | Valide | 200 | SUCCESS | |
| MOB-API-017 | ROLLS_VIDES avec quantite livree positive accepte en 1.3 avec trajet valide | sync-rolls-vides-livree-invalide.json | Valide | 200 | SUCCESS | |
| MOB-API-018 | ROLLS_VIDES avec quantite prevue positive accepte en 1.3 avec trajet valide | sync-rolls-vides-prevue-invalide.json | Valide | 200 | SUCCESS | |
| MOB-API-019 | GET camions disponibles valide | get-camions-disponibles.json | Valide | 200 | SUCCESS | |
| MOB-API-020 | Synchronisation 1.3 valide avec trajet camion | sync-v13-valide-trajet-camion.json | Valide | 200 | SUCCESS | |
| MOB-API-021 | Synchronisation 1.3 sans trajet | sync-v13-sans-trajet.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-022 | Synchronisation 1.3 sans camion | sync-v13-sans-camion.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-023 | Synchronisation 1.3 sans idCamion | sync-v13-sans-idcamion.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-024 | Synchronisation 1.3 sans kilometrageDepart | sync-v13-sans-kilometrage-depart.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-025 | Synchronisation 1.3 sans kilometrageArrivee | sync-v13-sans-kilometrage-arrivee.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-026 | Synchronisation 1.3 kilometrageDepart negatif | sync-v13-kilometrage-depart-negatif.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-027 | Synchronisation 1.3 kilometrageArrivee negatif | sync-v13-kilometrage-arrivee-negatif.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-028 | Synchronisation 1.3 kilometrageArrivee inferieur au depart | sync-v13-kilometrage-arrivee-inferieur-depart.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-029 | Synchronisation 1.3 sans dateDepartMobile | sync-v13-sans-date-depart-mobile.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-030 | Synchronisation 1.3 sans dateArriveeMobile | sync-v13-sans-date-arrivee-mobile.json | Invalide | 400 | VALIDATION_ERROR | |
| MOB-API-031 | Synchronisation schemaVersion 1.2 refusee | sync-v12-refusee.json | Invalide | 400 | VALIDATION_ERROR | |
