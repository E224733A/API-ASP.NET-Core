# Modèle de rapport - tests de masse MobileSLI

## Contexte

Date du test :

API testée :

Serveur :

Poste lanceur :

## Objectif

Vérifier que l'API MobileSLI accepte un volume contrôlé de synchronisations mobiles et que les données sont correctement enregistrées dans SQL Server.

## Paramètres de test

| Élément | Valeur |
|---|---|
| Nombre de synchronisations | 20 ou 50 |
| Nombre d'utilisateurs virtuels k6 | 5 ou 10 |
| Date tournée | |
| Préfixe code tournée | |
| Lignes par tournée | 5 |
| Articles par ligne | 4 |

## Résultats k6

| Indicateur | Valeur |
|---|---:|
| Requêtes HTTP | |
| Succès HTTP 200 | |
| Erreurs HTTP 400 | |
| Conflits HTTP 409 | |
| Erreurs HTTP 500 | |
| Temps moyen | |
| p95 | |

## Résultats SQL

| Contrôle | Résultat |
|---|---|
| Nombre de tournées | |
| Nombre de lignes | |
| Nombre de quantités | |
| Doublons | |
| Logs | |

## Conclusion

Le test est considéré comme réussi si le nombre de réponses HTTP 200 correspond au nombre de synchronisations attendues, s'il n'y a pas d'erreur serveur et si SQL Server contient les volumes attendus.
