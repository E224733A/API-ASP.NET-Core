# Changelog API

## 2026-05-18 - Stabilisation API + Expédition

### Ajouts

- Route `GET /api/expedition/preparations/a-preparer`
- Route `POST /api/expedition/preparations/verrouiller`
- Contrat JSON Expédition `schemaVersion = 1.2`
- Gestion du lot de verrouillage Expédition
- Idempotence par `idLotVerrouillage`
- Tests PowerShell Expédition
- Fichiers JSON de test Expédition

### Décisions métier

- Le GET Expédition est global.
- Aucun filtre GET n'est accepté.
- Le choix de tournée se fait côté Web Expédition.
- Le verrouillage se fait autour de `00:05`.
- Le fuseau métier est `Europe/Paris`.
- Les articles Expédition autorisés sont `ROLLS`, `TAPIS`, `SACS`.
- `ROLLS_VIDES` est interdit côté Expédition.
- Le mobile lit uniquement les préparations verrouillées.

### Corrections documentées

- Ajout des routes Expédition dans la documentation API.
- Ajout du contrat JSON Expédition.
- Correction de l'anti-doublon mobile : `DateTournee + CodeTournee`.
- Ajout des tests Expédition.
- Séparation claire entre documentation API, BDD, déploiement et tests.
