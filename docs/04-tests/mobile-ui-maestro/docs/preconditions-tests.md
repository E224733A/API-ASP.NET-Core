# Préconditions des tests UI Maestro

Les tests UI Maestro ne remplacent pas la préparation des données. Chaque scénario doit démarrer depuis un état connu.

## Précondition générale

```text
- application Mobile SLI installée sur le téléphone ou l'émulateur ;
- téléphone visible avec adb devices ;
- API centrale joignable si le scénario nécessite un chargement ;
- une tournée de test disponible ;
- application dans un état propre avant le scénario.
```

## MUI-001 - Reprise d'une tournée non synchronisée

### But

Vérifier que l'application propose clairement la reprise d'une tournée locale déjà chargée mais non synchronisée.

### Préparation manuelle

1. Lancer l'application mobile.
2. Charger une tournée.
3. Ouvrir au moins un point.
4. Faire une saisie simple.
5. Ne pas synchroniser la tournée.
6. Fermer puis rouvrir l'application.
7. Revenir sur le parcours de chargement de tournée.
8. Sélectionner une tournée.
9. Arriver sur l'écran de confirmation avec le bouton `Charger la tournée`.

### Résultat attendu

```text
- un panneau ou message signale qu'une tournée non synchronisée existe déjà ;
- le bouton de reprise est visible ;
- le bouton de reprise paraît cliquable ;
- le clic ouvre la liste des points de livraison de la tournée existante.
```

## MUI-002 - Saisie ROLLS_VIDES

### But

Vérifier que les chariots vides peuvent être saisis en livré côté mobile.

### Préparation manuelle

1. Charger une tournée contenant l'article `ROLLS_VIDES`.
2. Ouvrir l'écran `Points de livraison`.
3. Choisir un point qui contient `ROLLS_VIDES`.

### Résultat attendu

```text
- l'article est visible sous le libellé Rolls vides ou Chariots vides ;
- la colonne Livré contient un champ de saisie ;
- aucun tiret ne laisse penser que le champ n'est pas modifiable ;
- une quantité comme 3 peut être saisie ;
- la validation du passage ne bloque pas à cause de ROLLS_VIDES livré.
```

## MUI-003 - Validation d'un point

### But

Vérifier qu'un livreur peut valider un point simple.

### Préparation manuelle

1. Charger une tournée.
2. Ouvrir l'écran `Points de livraison`.
3. Choisir un point non validé.

### Résultat attendu

```text
- l'écran Détail point s'ouvre ;
- le statut Fait peut être sélectionné ;
- la validation enregistre le passage ;
- l'application revient à la liste des points ou affiche un état validé cohérent.
```

## Données recommandées

Utiliser une tournée de test contenant au minimum :

```text
- 1 point de livraison ;
- 1 article ROLLS ;
- 1 article ROLLS_VIDES ;
- 1 article TAPIS ou SACS ;
- aucun statut final déjà synchronisé.
```
