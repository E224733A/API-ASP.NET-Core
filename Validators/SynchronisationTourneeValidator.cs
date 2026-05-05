using System.Globalization;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Validators;

public sealed class SynchronisationTourneeValidator
{
    public List<string> Validate(SynchronisationTourneeRequest? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Le corps de la requête est obligatoire.");
            return errors;
        }

        ValidateHeader(request, errors);
        ValidateLignes(request.Lignes, errors);

        return errors;
    }

    private static void ValidateHeader(
        SynchronisationTourneeRequest request,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.SchemaVersion))
        {
            errors.Add("SchemaVersion est obligatoire.");
        }
        else if (request.SchemaVersion.Trim() != SchemaVersions.SynchronisationActuelle)
        {
            errors.Add($"SchemaVersion {request.SchemaVersion} n'est pas supportée. Version attendue : {SchemaVersions.SynchronisationActuelle}.");
        }

        if (string.IsNullOrWhiteSpace(request.IdSynchronisation))
        {
            errors.Add("IdSynchronisation est obligatoire.");
        }
        else if (!Guid.TryParse(request.IdSynchronisation, out _))
        {
            errors.Add("IdSynchronisation doit être un UUID valide.");
        }

        if (string.IsNullOrWhiteSpace(request.DateTournee))
        {
            errors.Add("DateTournee est obligatoire.");
        }
        else if (!DateOnly.TryParseExact(
                     request.DateTournee,
                     "yyyy-MM-dd",
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.None,
                     out _))
        {
            errors.Add("DateTournee est invalide. Format attendu : yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
        {
            errors.Add("CodeTournee est obligatoire.");
        }

        if (request.Livreur is null)
        {
            errors.Add("Livreur est obligatoire.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Livreur.CodeLivreur))
            {
                errors.Add("Livreur.CodeLivreur est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(request.Livreur.NomLivreur))
            {
                errors.Add("Livreur.NomLivreur est obligatoire.");
            }
        }

        ValidateMobile(request.Mobile, errors);
    }

    private static void ValidateMobile(
        SynchronisationMobileRequest? mobile,
        List<string> errors)
    {
        if (mobile is null)
        {
            errors.Add("Mobile est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(mobile.NomAppareil))
        {
            errors.Add("Mobile.NomAppareil est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(mobile.VersionApplication))
        {
            errors.Add("Mobile.VersionApplication est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(mobile.DateChargementMobile))
        {
            errors.Add("Mobile.DateChargementMobile est obligatoire.");
        }
        else if (!IsValidDateTime(mobile.DateChargementMobile))
        {
            errors.Add("Mobile.DateChargementMobile est invalide.");
        }

        if (string.IsNullOrWhiteSpace(mobile.DateEnvoiMobile))
        {
            errors.Add("Mobile.DateEnvoiMobile est obligatoire.");
        }
        else if (!IsValidDateTime(mobile.DateEnvoiMobile))
        {
            errors.Add("Mobile.DateEnvoiMobile est invalide.");
        }
    }

    private static void ValidateLignes(
        List<SynchronisationLigneRequest>? lignes,
        List<string> errors)
    {
        if (lignes is null || lignes.Count == 0)
        {
            errors.Add("La liste des lignes ne doit pas être vide.");
            return;
        }

        var idsLigneSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lignes.Count; i++)
        {
            var numeroLigne = i + 1;
            var ligne = lignes[i];

            if (ligne is null)
            {
                errors.Add($"Ligne {numeroLigne} : la ligne est obligatoire.");
                continue;
            }

            ValidateLigne(ligne, numeroLigne, idsLigneSource, errors);
        }
    }

    private static void ValidateLigne(
        SynchronisationLigneRequest ligne,
        int numeroLigne,
        HashSet<string> idsLigneSource,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
        {
            errors.Add($"Ligne {numeroLigne} : IdLigneSource est obligatoire.");
        }
        else
        {
            var idLigneSource = ligne.IdLigneSource.Trim();

            if (!idsLigneSource.Add(idLigneSource))
            {
                errors.Add($"Ligne {numeroLigne} : IdLigneSource est dupliqué dans la requête.");
            }
        }

        if (ligne.OrdreArret < 0)
        {
            errors.Add($"Ligne {numeroLigne} : OrdreArret ne peut pas être négatif.");
        }

        ValidateClient(ligne.Client, numeroLigne, errors);
        ValidatePointLivraison(ligne.PointLivraison, numeroLigne, errors);
        ValidateSaisie(ligne.Saisie, numeroLigne, errors);
    }

    private static void ValidateClient(
        SynchronisationClientRequest? client,
        int numeroLigne,
        List<string> errors)
    {
        if (client is null)
        {
            errors.Add($"Ligne {numeroLigne} : Client est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(client.NumClient))
        {
            errors.Add($"Ligne {numeroLigne} : Client.NumClient est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(client.NomClient))
        {
            errors.Add($"Ligne {numeroLigne} : Client.NomClient est obligatoire.");
        }
    }

    private static void ValidatePointLivraison(
        SynchronisationPointLivraisonRequest? pointLivraison,
        int numeroLigne,
        List<string> errors)
    {
        if (pointLivraison is null)
        {
            errors.Add($"Ligne {numeroLigne} : PointLivraison est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(pointLivraison.CodePDL))
        {
            errors.Add($"Ligne {numeroLigne} : PointLivraison.CodePDL est obligatoire.");
        }
    }

    private static void ValidateSaisie(
        SynchronisationSaisieRequest? saisie,
        int numeroLigne,
        List<string> errors)
    {
        if (saisie is null)
        {
            errors.Add($"Ligne {numeroLigne} : Saisie est obligatoire.");
            return;
        }

        var statut = saisie.StatutPassage?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(statut))
        {
            errors.Add($"Ligne {numeroLigne} : StatutPassage est obligatoire.");
        }
        else if (!StatutsPassage.Tous.Contains(statut))
        {
            errors.Add($"Ligne {numeroLigne} : StatutPassage doit être {StatutsPassage.AFaire}, {StatutsPassage.Fait}, {StatutsPassage.NonFait} ou {StatutsPassage.Anomalie}.");
        }
        else if (!StatutsPassage.AutorisesEnvoiFinal.Contains(statut))
        {
            errors.Add($"Ligne {numeroLigne} : {StatutsPassage.AFaire} est interdit dans l'envoi final.");
        }

        if (!saisie.EstValidee)
        {
            errors.Add($"Ligne {numeroLigne} : EstValidee doit être true pour l'envoi final.");
        }

        if (saisie.EstValidee && string.IsNullOrWhiteSpace(saisie.HeureValidation))
        {
            errors.Add($"Ligne {numeroLigne} : HeureValidation est obligatoire.");
        }

        if (!string.IsNullOrWhiteSpace(saisie.HeureValidation)
            && !IsValidDateTimeOrTime(saisie.HeureValidation))
        {
            errors.Add($"Ligne {numeroLigne} : HeureValidation est invalide.");
        }

        if ((string.Equals(statut, StatutsPassage.NonFait, StringComparison.OrdinalIgnoreCase)
             || string.Equals(statut, StatutsPassage.Anomalie, StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
        {
            errors.Add($"Ligne {numeroLigne} : CommentaireLivreur est obligatoire pour le statut {statut}.");
        }

        ValidateQuantites(saisie.Quantites, numeroLigne, errors);
    }

    private static void ValidateQuantites(
        List<SynchronisationQuantiteRequest>? quantites,
        int numeroLigne,
        List<string> errors)
    {
        if (quantites is null || quantites.Count == 0)
        {
            errors.Add($"Ligne {numeroLigne} : Quantites doit contenir au moins un article.");
            return;
        }

        var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < quantites.Count; i++)
        {
            var numeroArticle = i + 1;
            var quantite = quantites[i];

            if (quantite is null)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : l'article est obligatoire.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : CodeArticle est obligatoire.");
            }
            else
            {
                var codeArticle = quantite.CodeArticle.Trim().ToUpperInvariant();

                if (!codesArticles.Add(codeArticle))
                {
                    errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : CodeArticle {codeArticle} est dupliqué.");
                }
            }

            if (quantite.QuantiteLivree < 0)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : QuantiteLivree ne peut pas être négative.");
            }

            if (quantite.QuantiteRecuperee < 0)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : QuantiteRecuperee ne peut pas être négative.");
            }
        }
    }

    private static bool IsValidDateTime(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
               || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _);
    }

    private static bool IsValidDateTimeOrTime(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
               || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _)
               || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out _);
    }
}