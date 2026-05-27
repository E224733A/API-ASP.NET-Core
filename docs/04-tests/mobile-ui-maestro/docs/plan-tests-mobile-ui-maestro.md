# Plan de tests UI mobile - Maestro

## Objectif

Automatiser quelques parcours critiques de l'application Mobile SLI afin de détecter rapidement les régressions d'interface.

Ces tests complètent :

```text
- les tests API unitaires et fonctionnels ;
- les tests de masse k6 ;
- les vérifications SQL ;
- la recette manuelle sur vrai téléphone.
```

## Cas de test

| ID | Nom | Précondition | Action principale | Résultat attendu |
|---|---|---|---|---|
| MUI-001 | Reprise tournée | Une tournée locale non synchronisée existe | Cliquer sur Reprendre | La liste des points s'ouvre. |
| MUI-002 | Saisie ROLLS_VIDES | Un point contient ROLLS_VIDES | Saisir 3 en livré | La valeur est acceptée et aucun tiret ne bloque l'utilisateur. |
| MUI-003 | Validation point | Un point est à faire | Sélectionner Fait puis valider | Le passage est enregistré. |

## Critères de réussite

Un test UI est considéré réussi si :

```text
- le flow Maestro se termine sans erreur ;
- l'écran attendu apparaît ;
- le comportement métier est cohérent ;
- aucune erreur visible ne bloque le livreur ;
- le test est reproductible sur un deuxième lancement.
```

## Limites connues

Les tests UI peuvent échouer pour des raisons d'environnement :

```text
- téléphone non détecté ;
- application non installée ;
- données de test absentes ;
- API indisponible ;
- texte d'interface modifié ;
- clavier Android qui masque le bouton ;
- lenteur de chargement.
```

Ces échecs ne sont pas toujours des bugs applicatifs. Le rapport doit distinguer :

```text
- erreur de test ;
- erreur d'environnement ;
- vraie régression applicative.
```

## Justification pour l'IUT

Ces tests montrent que le projet ne repose pas uniquement sur des tests manuels. Ils démontrent une démarche de validation plus professionnelle :

```text
exigence métier -> scénario UI -> précondition -> exécution automatisée -> trace -> décision.
```
