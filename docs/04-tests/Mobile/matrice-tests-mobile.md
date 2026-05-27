# Matrice de tests API Mobile SLI v1.2

| ID | Scenario | Fichier | Type | HTTP attendu | Statut attendu | Code attendu |
|---|---|---|---|---:|---|---|
| MOB-API-001 | Synchronisation valide | sync-valide.json | Valide | 200 | SUCCESS | |
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
| MOB-API-015 | quantiteLivreePrevue zero acceptee | sync-prevu-zero.json | Valide | 200 | SUCCESS | |
| MOB-API-016 | ROLLS_VIDES livre accepte | sync-valide-rolls-vides.json | Valide | 200 | SUCCESS | |
| MOB-API-017 | ROLLS_VIDES avec quantite livree positive accepte | sync-rolls-vides-livree-invalide.json | Valide | 200 | SUCCESS | |
| MOB-API-018 | ROLLS_VIDES avec quantite prevue positive accepte | sync-rolls-vides-prevue-invalide.json | Valide | 200 | SUCCESS | |
