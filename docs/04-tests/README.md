# 04 - Tests API

Ce dossier contient les tests manuels et semi-automatiques de l'API.

## Dossiers

| Dossier | Rôle |
|---|---|
| `Expedition` | Tests des routes Expédition |
| `Mobile` | Tests des routes mobile |

## Règle

Les tests GET ne modifient pas la base.

Les tests POST peuvent écrire en base de développement.

## Lancement recommandé

1. Lancer l'API.
2. Exécuter les tests GET.
3. Exécuter les tests POST uniquement sur une base de développement.
4. Vérifier les données SQL après les POST.
