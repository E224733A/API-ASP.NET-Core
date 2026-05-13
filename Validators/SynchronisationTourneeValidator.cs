using API_ASP.NET_Core.Models;
using System.Globalization;

namespace API_ASP.NET_Core.Validators;

public sealed class SynchronisationTourneeValidator
{
    private const string CodeArticleRollsVides = "ROLLS_VIDES";

    private static readonly HashSet<string> StatutsAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAIT",
        "NON_FAIT",
        "ANOMALIE"
    };

    public SynchronisationValidationResult Validate(SynchronisationTourneeRequest? request)
    {
        var errors = new List<SynchronisationValidationError>();

        if (request is null)
        {
            errors.Add(new SynchronisationValidationError("body", "Le corps JSON est obligatoire."));
            return new SynchronisationValidationResult(errors);
        }

        if (string.IsNullOrWhiteSpace(request.SchemaVersion))
        {
            errors.Add(new SynchronisationValidationError("schemaVersion", "La version de schéma est obligatoire."));
        }
        else if (!string.Equals(request.SchemaVersion.Trim(), "1.2", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SynchronisationValidationError("schemaVersion", "La version de schéma supportée est 1.2."));
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

                /*
                 * Règle métier ROLLS_VIDES :
                 * cet article sert uniquement à distinguer les rolls vides récupérés.
                 * Il ne doit jamais être livré au client et ne doit pas avoir
                 * de quantité livrée prévue positive.
                 */
                if (IsRollsVides(quantite.CodeArticle))
                {
                    if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value > 0)
                    {
                        errors.Add(new SynchronisationValidationError(
                            $"{quantitePrefix}.quantiteLivreePrevue",
                            "Les rolls vides ne doivent pas avoir de quantité livrée prévue."));
                    }

                    if (quantite.QuantiteLivree != 0)
                    {
                        errors.Add(new SynchronisationValidationError(
                            $"{quantitePrefix}.quantiteLivree",
                            "L'article ROLLS_VIDES est uniquement récupéré : la quantité livrée doit être égale à 0."));
                    }
                }
            }
        }

        return new SynchronisationValidationResult(errors);
    }

    public static bool IsRollsVides(string? codeArticle)
    {
        return string.Equals(codeArticle?.Trim(), CodeArticleRollsVides, StringComparison.OrdinalIgnoreCase);
    }

    public static DateTime ParseDateTournee(object? value)
    {
        if (TryParseDateTournee(value, out var dateTournee))
        {
            return dateTournee;
        }

        throw new InvalidOperationException("La date de tournée est invalide.");
    }

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

    public static bool TryParseGuid(object? value, out Guid guid)
    {
        if (value is Guid existingGuid)
        {
            guid = existingGuid;
            return true;
        }

        return Guid.TryParse(Convert.ToString(value), out guid);
    }

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

public sealed class SynchronisationValidationResult
{
    public SynchronisationValidationResult(IReadOnlyList<SynchronisationValidationError> errors)
    {
        Errors = errors;
    }

    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<SynchronisationValidationError> Errors { get; }
}

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
