# Rapport d'exécution - Tests UI Maestro Mobile SLI

Date d'exécution : à compléter  
Version application : à compléter  
Téléphone ou émulateur : à compléter  
API utilisée : à compléter  
Dossier de tests : `API-ASP.NET-Core/docs/04-tests/mobile-ui-maestro`

## Synthèse

| ID | Scénario | Résultat | Commentaire |
|---|---|---|---|
| MUI-001 | Reprise d'une tournée non synchronisée | À compléter | À compléter |
| MUI-002 | Saisie ROLLS_VIDES | À compléter | À compléter |
| MUI-003 | Validation d'un point | À compléter | À compléter |

## Détail MUI-001 - Reprise tournée

Précondition vérifiée : oui / non  
Flow utilisé : `flows-texte/01-reprise-tournee.yaml` ou `flows-stables-automationid/01-reprise-tournee.yaml`  
Résultat Maestro : succès / échec  
Analyse : à compléter

## Détail MUI-002 - Saisie ROLLS_VIDES

Précondition vérifiée : oui / non  
Flow utilisé : `flows-texte/02-saisie-rolls-vides.yaml` ou `flows-stables-automationid/02-saisie-rolls-vides.yaml`  
Résultat Maestro : succès / échec  
Analyse : à compléter

Points à vérifier manuellement en complément :

```text
- le champ Livré est bien éditable ;
- aucun tiret n'est affiché dans la colonne Livré ;
- la quantité saisie est conservée ;
- la validation du passage fonctionne.
```

## Détail MUI-003 - Validation point

Précondition vérifiée : oui / non  
Flow utilisé : `flows-texte/03-validation-point.yaml` ou `flows-stables-automationid/03-validation-point.yaml`  
Résultat Maestro : succès / échec  
Analyse : à compléter

## Conclusion

À compléter.

Exemple de conclusion :

```text
Les trois scénarios UI critiques ont été exécutés. Les parcours de reprise de tournée, de saisie ROLLS_VIDES et de validation d'un point sont utilisables. Les tests doivent être rejoués après chaque correction d'interface mobile.
```
