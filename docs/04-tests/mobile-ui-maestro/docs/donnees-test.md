# Données de test recommandées

## Tournée de test minimale

Pour les tests UI Maestro, utiliser une tournée simple avec au moins un point.

```text
Code tournée : TEST_UI_MOBILE
Livreur      : livreur de test existant
Date tournée : date métier mobile du jour
Nombre points: 1 à 3
```

## Point de livraison recommandé

```text
Client          : CLIENT TEST UI
Point livraison : PDL TEST UI
Statut initial  : A_FAIRE
```

## Articles recommandés

| Code article | Libellé | Quantité livrée prévue | Quantité livrée testée | Quantité récupérée testée |
|---|---|---:|---:|---:|
| ROLLS | Rolls | 2 | 2 | 1 |
| ROLLS_VIDES | Chariots vides | 0 ou 2 | 3 | 1 |
| TAPIS | Tapis | 0 | 0 | 0 |
| SACS | Sacs | 0 | 0 | 0 |

## Cas ROLLS_VIDES

Le test MUI-002 doit vérifier que :

```text
- ROLLS_VIDES apparaît dans la liste des quantités ;
- le champ Livré est un champ de saisie ;
- il n'y a plus de tiret à la place du champ ;
- la valeur 3 peut être saisie ;
- le passage peut être validé.
```

## Préparation rapide

La préparation peut être faite de deux façons :

```text
1. Par l'API réelle, avec une tournée contenant ROLLS_VIDES.
2. Par une base SQLite locale déjà préparée pour la recette mobile.
```

Pour la soutenance, la première option est plus réaliste. Pour les tests répétés, une base SQLite de test est plus rapide.
