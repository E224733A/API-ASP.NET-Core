using API_ASP.NET_Core.Models;
using System.Globalization;

namespace API_ASP.NET_Core.Validators;

/// <summary>
/// Validator du contrat JSON de synchronisation finale envoyé par le mobile.
/// </summary>
/// <remarks>
/// Ce validator protège l'API avant toute écriture SQL : version de schéma, identifiant
/// de synchronisation, date de tournée, livreur, trajet camion, lignes, statuts et quantités.
/// La version 1.2 est volontairement refusée pour le POST final, car le contrat 1.3 impose
/// désormais les informations de trajet camion.
/// </remarks>
public sealed class SynchronisationTourneeValidator
{
    private const string SchemaVersionHistoriqueRefusee = "1.2";
    private const string SchemaVersionAvecTrajet = "1.3";

    private static readonly HashSet<string> StatutsAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAIT",
        "NON_FAIT",
        "ANOMALIE"
    };

    /// <summary>
    /// Valide l'ensemble du payload mobile avant traitement métier et persistance.
    /// </summary>
    /// <remarks>
    /// Les erreurs sont accumulées afin de renvoyer au mobile une liste complète des champs
    /// incorrects plutôt qu'une seule erreur bloquante à la fois.
    /// </remarks>
    public SynchronisationValidationResult Validate(SynchronisationTourneeRequest? request)
    {
        var errors = new List<SynchronisationValidationError>();

        if (request is null)
        {
            errors.Add(new SynchronisationValidationError("body", "Le corps JSON est obligatoire."));
            return new SynchronisationValidationResult(errors);
        }

        var schemaVersion = request.SchemaVersion?.Trim();

        if (string.IsNullOrWhiteSpace(schemaVersion))
        {
            errors.Add(new SynchronisationValidationError("schemaVersion", "La version de schéma est obligatoire."));
        }
        else if (string.Equals(schemaVersion, SchemaVersionHistoriqueRefusee, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SynchronisationValidationError("schemaVersion", "La version de schéma 1.2 n'est plus acceptée pour la synchronisation. La version supportée est 1.3."));
        }
        else if (!string.Equals(schemaVersion, SchemaVersionAvecTrajet, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SynchronisationValidationError("schemaVersion", "La version de schéma supportée est 1.3."));
        }

        if (string.Equals(schemaVersion, SchemaVersionAvecTrajet, StringComparison.OrdinalIgnoreCase))
        {
            // Contrat 1.3 : le trajet camion fait partie du POST final et devient obligatoire.
            ValidateTrajet(request.Trajet, errors);
        }

        if (!TryParseGuid(request.IdSynchronisation, out _))
        {
            errors.Add(new SynchronisationValidationError("idSynchronisation", "L'identifiant de synchronisation est obligatoire et doit être un GUID valide."));
        }

        if (!TryParseDateTournee(request.DateTournee, out _))
        {
            errors.Add(new SynchronisationValidationError("dateTournee", "La date de tournée est obligatoire et doit être valide."));
        }

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
        {
            errors.Add(new SynchronisationValidationError("codeTournee", "Le code tournée est obligatoire."));
        }

        if (request.Livreur is null)
        {
            errors.Add(new SynchronisationValidationError("livreur", "L'objet livreur est obligatoire."));
        }
        else if (string.IsNullOrWhiteSpace(request.Livreur.CodeLivreur))
        {
            errors.Add(new SynchronisationValidationError("livreur.codeLivreur", "Le code livreur est obligatoire."));
        }

        if (request.Mobile is null)
        {
            errors.Add(new SynchronisationValidationError("mobile", "L'objet mobile est obligatoire."));
        }

        if (request.Lignes is null || request.Lignes.Count == 0)
        {
            errors.Add(new SynchronisationValidationError("lignes", "La synchronisation doit contenir au moins une ligne."));
            return new SynchronisationValidationResult(errors);
        }

        var idsLignes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var indexLigne = 0; indexLigne < request.Lignes.Count; indexLigne++)
        {
            var ligne = request.Lignes[indexLigne];
            var prefix = $"lignes[{indexLigne}]";

            if (ligne is null)
            {
                errors.Add(new SynchronisationValidationError(prefix, "La ligne est obligatoire."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.idLigneSource", "L'identifiant source de ligne est obligatoire."));
            }
            else if (!idsLignes.Add(ligne.IdLigneSource.Trim()))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.idLigneSource", "L'identifiant source de ligne est présent plusieurs fois dans la synchronisation."));
            }

            if (ligne.Saisie is null)
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie", "L'objet saisie est obligatoire."));
                continue;
            }

            var statut = ligne.Saisie.StatutPassage?.Trim();

            if (string.IsNullOrWhiteSpace(statut))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.statutPassage", "Le statut de passage est obligatoire."));
            }
            else if (string.Equals(statut, "A_FAIRE", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.statutPassage", "Le statut A_FAIRE ne doit pas être envoyé dans la synchronisation finale."));
            }
            else if (!StatutsAutorises.Contains(statut))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.statutPassage", "Le statut de passage doit être FAIT, NON_FAIT ou ANOMALIE."));
            }

            // Règle métier : un arrêt non réalisé ou en anomalie doit être expliqué par le livreur.
            if ((string.Equals(statut, "NON_FAIT", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(statut, "ANOMALIE", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(ligne.Saisie.CommentaireLivreur))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.commentaireLivreur", "Le commentaire livreur est obligatoire pour le statut NON_FAIT ou ANOMALIE."));
            }

            if (!ligne.Saisie.EstValidee)
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.estValidee", "Chaque ligne envoyée doit être validée."));
            }

            if (!TryParseDateTimeOffsetNullable(ligne.Saisie.HeureValidation, out _))
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.heureValidation", "L'heure de validation est obligatoire et doit être valide."));
            }

            if (ligne.Saisie.Quantites is null || ligne.Saisie.Quantites.Count == 0)
            {
                errors.Add(new SynchronisationValidationError($"{prefix}.saisie.quantites", "Chaque ligne doit contenir au moins une quantité."));
                continue;
            }

            var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var indexQuantite = 0; indexQuantite < ligne.Saisie.Quantites.Count; indexQuantite++)
            {
                var quantite = ligne.Saisie.Quantites[indexQuantite];
                var quantitePrefix = $"{prefix}.saisie.quantites[{indexQuantite}]";

                if (quantite is null)
                {
                    errors.Add(new SynchronisationValidationError(quantitePrefix, "La quantité est obligatoire."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
                {
                    errors.Add(new SynchronisationValidationError($"{quantitePrefix}.codeArticle", "Le code article est obligatoire."));
                }
                else if (!codesArticles.Add(quantite.CodeArticle.Trim()))
                {
                    errors.Add(new SynchronisationValidationError($"{quantitePrefix}.codeArticle", "Le code article est présent plusieurs fois dans une même ligne."));
                }

                if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value < 0)
                {
                    errors.Add(new SynchronisationValidationError($"{quantitePrefix}.quantiteLivreePrevue", "La quantité livrée prévue doit être positive ou nulle."));
                }

                if (quantite.QuantiteLivree < 0)
                {
                    errors.Add(new SynchronisationValidationError($"{quantitePrefix}.quantiteLivree", "La quantité livrée doit être positive ou nulle."));
                }

                if (quantite.QuantiteRecuperee < 0)
                {
                    errors.Add(new SynchronisationValidationError($"{quantitePrefix}.quantiteRecuperee", "La quantité récupérée doit être positive ou nulle."));
                }
            }
        }

        return new SynchronisationValidationResult(errors);
    }

    /// <summary>
    /// Valide le trajet camion obligatoire dans le contrat de synchronisation mobile 1.3.
    /// </summary>
    /// <remarks>
    /// Les kilométrages et les dates mobiles sont contrôlés ici afin que le repository puisse
    /// persister le trajet sans recalculer les règles de cohérence.
    /// </remarks>
    private static void ValidateTrajet(
        SynchronisationTrajetRequest? trajet,
        ICollection<SynchronisationValidationError> errors)
    {
        if (trajet is null)
        {
            errors.Add(new SynchronisationValidationError("trajet", "trajet obligatoire"));
            return;
        }

        if (trajet.Camion is null)
        {
            errors.Add(new SynchronisationValidationError("trajet.camion", "trajet.camion obligatoire"));
        }
        else if (string.IsNullOrWhiteSpace(trajet.Camion.IdCamion))
        {
            errors.Add(new SynchronisationValidationError("trajet.camion.idCamion", "trajet.camion.idCamion obligatoire"));
        }

        if (!trajet.KilometrageDepart.HasValue)
        {
            errors.Add(new SynchronisationValidationError("trajet.kilometrageDepart", "trajet.kilometrageDepart obligatoire"));
        }
        else if (trajet.KilometrageDepart.Value < 0)
        {
            errors.Add(new SynchronisationValidationError("trajet.kilometrageDepart", "Le kilométrage de départ doit être positif ou nul."));
        }

        if (!trajet.KilometrageArrivee.HasValue)
        {
            errors.Add(new SynchronisationValidationError("trajet.kilometrageArrivee", "trajet.kilometrageArrivee obligatoire"));
        }
        else if (trajet.KilometrageArrivee.Value < 0)
        {
            errors.Add(new SynchronisationValidationError("trajet.kilometrageArrivee", "Le kilométrage d'arrivée doit être positif ou nul."));
        }

        if (trajet.KilometrageDepart.HasValue
            && trajet.KilometrageArrivee.HasValue
            && trajet.KilometrageArrivee.Value >= 0
            && trajet.KilometrageDepart.Value >= 0
            && trajet.KilometrageArrivee.Value < trajet.KilometrageDepart.Value)
        {
            errors.Add(new SynchronisationValidationError("trajet.kilometrageArrivee", "Le kilométrage d'arrivée doit être supérieur ou égal au kilométrage de départ."));
        }

        var dateDepartValide = TryParseDateTimeOffsetNullable(trajet.DateDepartMobile, out var dateDepartMobile);
        var dateArriveeValide = TryParseDateTimeOffsetNullable(trajet.DateArriveeMobile, out var dateArriveeMobile);

        if (!dateDepartValide)
        {
            errors.Add(new SynchronisationValidationError("trajet.dateDepartMobile", "trajet.dateDepartMobile obligatoire"));
        }

        if (!dateArriveeValide)
        {
            errors.Add(new SynchronisationValidationError("trajet.dateArriveeMobile", "trajet.dateArriveeMobile obligatoire"));
        }

        if (dateDepartValide
            && dateArriveeValide
            && dateDepartMobile.HasValue
            && dateArriveeMobile.HasValue
            && dateArriveeMobile.Value < dateDepartMobile.Value)
        {
            errors.Add(new SynchronisationValidationError("trajet.dateArriveeMobile", "La date d'arrivée mobile doit être supérieure ou égale à la date de départ mobile."));
        }
    }

    /// <summary>
    /// Convertit une date de tournée déjà validée ou lève une erreur interne si elle reste invalide.
    /// </summary>
    public static DateTime ParseDateTournee(object? value)
    {
        if (TryParseDateTournee(value, out var dateTournee))
        {
            return dateTournee;
        }

        throw new InvalidOperationException("La date de tournée est invalide.");
    }

    /// <summary>
    /// Tente de lire la date de tournée depuis les formats acceptés par le contrat JSON.
    /// </summary>
    public static bool TryParseDateTournee(object? value, out DateTime dateTournee)
    {
        if (value is DateTime dateTime)
        {
            dateTournee = dateTime.Date;
            return true;
        }

        var text = Convert.ToString(value)?.Trim();

        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            dateTournee = parsedDate.Date;
            return true;
        }

        if (DateTime.TryParse(
                text,
                CultureInfo.GetCultureInfo("fr-FR"),
                DateTimeStyles.None,
                out parsedDate))
        {
            dateTournee = parsedDate.Date;
            return true;
        }

        dateTournee = default;
        return false;
    }

    /// <summary>
    /// Tente de convertir une valeur JSON en GUID de synchronisation.
    /// </summary>
    public static bool TryParseGuid(object? value, out Guid guid)
    {
        if (value is Guid existingGuid)
        {
            guid = existingGuid;
            return true;
        }

        return Guid.TryParse(Convert.ToString(value), out guid);
    }

    /// <summary>
    /// Tente de convertir une date mobile optionnelle avec offset.
    /// </summary>
    /// <remarks>
    /// Le retour false permet au validator de signaler précisément les champs de date absents
    /// ou invalides sans déclencher d'exception pendant la validation du payload.
    /// </remarks>
    public static bool TryParseDateTimeOffsetNullable(object? value, out DateTimeOffset? dateTimeOffset)
    {
        if (value is null)
        {
            dateTimeOffset = null;
            return false;
        }

        if (value is DateTimeOffset existingDateTimeOffset)
        {
            dateTimeOffset = existingDateTimeOffset;
            return true;
        }

        if (value is DateTime existingDateTime)
        {
            dateTimeOffset = new DateTimeOffset(existingDateTime);
            return true;
        }

        var text = Convert.ToString(value)?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            dateTimeOffset = null;
            return false;
        }

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDateTimeOffset))
        {
            dateTimeOffset = parsedDateTimeOffset;
            return true;
        }

        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDateTime))
        {
            dateTimeOffset = new DateTimeOffset(parsedDateTime);
            return true;
        }

        dateTimeOffset = null;
        return false;
    }
}

/// <summary>
/// Résultat de validation du payload de synchronisation mobile.
/// </summary>
public sealed class SynchronisationValidationResult
{
    public SynchronisationValidationResult(IReadOnlyList<SynchronisationValidationError> errors)
    {
        Errors = errors;
    }

    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<SynchronisationValidationError> Errors { get; }
}

/// <summary>
/// Erreur de validation associée à un champ précis du contrat JSON mobile.
/// </summary>
public sealed class SynchronisationValidationError
{
    public SynchronisationValidationError(string field, string message)
    {
        Field = field;
        Message = message;
    }

    public string Field { get; }

    public string Message { get; }
}
