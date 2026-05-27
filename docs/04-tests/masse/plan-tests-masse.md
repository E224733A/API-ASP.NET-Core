# Plan de tests de masse - API Mobile SLI

## Objectif

Valider le comportement de l'API centrale lorsqu'elle reçoit plusieurs synchronisations mobiles sur une période courte.

Le but n'est pas de reproduire une charge Internet massive, mais de simuler un retour dépôt réaliste : plusieurs téléphones peuvent envoyer leurs tournées au même moment sur le réseau interne.

## Hypothèses de test

| Élément | Hypothèse |
|---|---|
| Route testée | `POST /api/synchronisations` |
| Contrat JSON | `schemaVersion = 1.2` |
| Date métier mobile | date du jour côté Europe/Paris |
| Nombre de lignes par tournée simulée | 5 |
| Nombre d'articles par ligne | 4 |
| Articles utilisés | `ROLLS`, `ROLLS_VIDES`, `TAPIS`, `SACS` |
| Type de synchronisation principale | synchronisations valides |
| Vérification après test | SQL Server, tables `Mobile_*` |

## Cas de test

| ID | Nom | Volume | Objectif | Critère de réussite |
|---|---:|---:|---|---|
| MASSE-API-001 | Synchronisations mobiles valides - court | 20 POST | Vérifier un retour dépôt simple | 20 succès HTTP 200, aucune erreur 500 |
| MASSE-API-002 | Synchronisations mobiles valides - large | 50 POST | Vérifier la robustesse sur un volume plus valorisant | 50 succès HTTP 200, p95 inférieur à 2 secondes si le réseau est normal |
| MASSE-API-003 | Vérification SQL après masse | 20 ou 50 tournées | Vérifier que les données reçues sont réellement sauvegardées | nombre de tournées, lignes et quantités conforme |
| MASSE-API-004 | Mélange valides et invalides | paramétrable | Vérifier que les erreurs métier restent propres sous volume | 400 attendus pour invalides, pas de 500 |

## Données générées

Chaque synchronisation reçoit :

```text
idSynchronisation unique
codeTournee unique avec préfixe K6
5 lignes de livraison
4 quantités par ligne
statutPassage = FAIT
estValidee = true
heureValidation renseignée
```

Exemple de code tournée généré :

```text
K6841530001
K6841530002
K6841530003
```

Le préfixe est enregistré dans `resultats/metadata-latest.json` pour permettre la vérification SQL.

## Indicateurs mesurés

| Indicateur | Rôle |
|---|---|
| `http_reqs` | Nombre total de requêtes envoyées |
| `http_req_duration.avg` | Temps moyen de réponse |
| `http_req_duration.p(95)` | Temps sous lequel 95 % des réponses passent |
| `mobile_sync_success_200` | Nombre de synchronisations acceptées |
| `mobile_sync_validation_400` | Nombre d'erreurs de validation fonctionnelles |
| `mobile_sync_conflict_409` | Nombre de conflits métier |
| `mobile_sync_unexpected` | Nombre de réponses inattendues |

## Critères d'acceptation

Pour le scénario valide :

```text
- 100 % des synchronisations attendues retournent HTTP 200.
- Aucune réponse HTTP 500.
- Aucune réponse inattendue.
- Les données sont présentes en SQL Server après exécution.
- Les nombres SQL correspondent au volume simulé.
```

## Limites volontaires

Ces tests ne remplacent pas :

- les tests manuels sur téléphone Android ;
- les tests hors connexion SQLite ;
- les tests de l'interface mobile ;
- les tests de verrouillage Expédition.

Ils complètent la recette en validant le comportement de l'API et de la base sous volume contrôlé.
