using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Mappers;

namespace API_ASP.NET_Core.Validators;

/// <summary>
/// Validator pour la requête de verrouillage Expédition.
/// </summary>
/// <remarks>
/// Ce composant isole les règles de validation du payload ServeWeb avant l'appel au service
/// de verrouillage et à la persistance SQL. Il vérifie le contrat JSON Expédition v1.2,
/// la source attendue, le fuseau métier, les lignes préparables, les statuts web et les
/// articles autorisés côté Expédition.
/// </remarks>
public sealed class ExpeditionVerrouillageValidator
{
    // Contrat JSON Expédition v1.2 : ces valeurs sont attendues telles quelles depuis ServeWeb.
    private const string SchemaVersionExpedition = "1.2";
    private const string SourceAttendue = "APPLICATION_WEB_EXPEDITION";
    private const string FuseauHoraireMetier = "Europe/Paris";

    // Articles préparables côté Expédition. ROLLS_VIDES est explicitement autorisé comme quantité prévue.
    private static readonly HashSet<string> ArticlesAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROLLS",
        "ROLLS_VIDES",
        "TAPIS",
        "SACS"
    };

    // Statuts acceptés depuis ServeWeb avant verrouillage API.
    private static readonly HashSet<string> StatutsPreparationWebAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRETE_VERROUILLAGE",
        "EN_PREPARATION_WEB"
    };

    private readonly TourneesRepository _tourneesRepository;
    private readonly DateMetierService _dateMetierService;

    /// <summary>
    /// Initialise une nouvelle instance du validator.
    /// </summary>
    /// <param name="tourneesRepository">Dépôt pour accéder aux lignes préparables.</param>
    /// <param name="dateMetierService">Service pour obtenir la date métier actuelle.</param>
    public ExpeditionVerrouillageValidator(
        TourneesRepository tourneesRepository,
        DateMetierService dateMetierService)
    {
        _tourneesRepository = tourneesRepository;
        _dateMetierService = dateMetierService;
    }

    /// <summary>
    /// Valide la requête de verrouillage Expédition et retourne toutes les erreurs détectées.
    /// </summary>
    /// <remarks>
    /// Le validator ne verrouille rien lui-même. Il sécurise le payload avant orchestration :
    /// corps JSON, version, source, dates, fuseau, tournées, lignes et quantités prévues.
    /// </remarks>
    /// <param name="request">Requête à valider.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Liste des messages d'erreur. Vide si aucune erreur.</returns>
    public async Task<IReadOnlyList<string>> ValidateAsync(
        ExpeditionVerrouillageLotRequest? request,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        DateTime? dateTourneeValide = null;
        DateTimeOffset? dateVerrouillageDemandeeValide = null;

        if (request is null)
        {
            errors.Add("Le corps JSON du lot Expédition est obligatoire.");
            return errors;
        }

        if (!string.Equals(request.SchemaVersion?.Trim(), SchemaVersionExpedition, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("schemaVersion doit être égal à \"1.2\".");
        }

        if (string.IsNullOrWhiteSpace(request.IdLotVerrouillage))
        {
            errors.Add("idLotVerrouillage est obligatoire.");
        }

        if (!string.Equals(request.Source?.Trim(), SourceAttendue, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"source doit être égal à \"{SourceAttendue}\".");
        }

        if (!DateTime.TryParse(request.DateTournee, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTournee))
        {
            errors.Add("dateTournee est obligatoire et doit être une date valide au format yyyy-MM-dd.");
        }
        else
        {
            dateTourneeValide = dateTournee.Date;
        }

        if (!DateTimeOffset.TryParse(request.DateVerrouillageDemandee, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateVerrouillageDemandee))
        {
            errors.Add("dateVerrouillageDemandee est obligatoire et doit être une date ISO 8601 avec offset.");
        }
        else if (!HasExplicitOffset(request.DateVerrouillageDemandee))
        {
            errors.Add("dateVerrouillageDemandee doit contenir un offset horaire explicite.");
        }
        else
        {
            dateVerrouillageDemandeeValide = dateVerrouillageDemandee;
        }

        if (!string.Equals(request.FuseauHoraireMetier?.Trim(), FuseauHoraireMetier, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"fuseauHoraireMetier doit être égal à \"{FuseauHoraireMetier}\".");
        }

        if (request.Tournees is null || request.Tournees.Count == 0)
        {
            errors.Add("tournees doit contenir au moins une tournée.");
            return errors;
        }

        HashSet<string>? idLignesPreparables = null;
        if (dateTourneeValide.HasValue)
        {
            var dateAutorisee = _dateMetierService.GetDateTourneeExpeditionPreparable().ToDateTime(TimeOnly.MinValue).Date;

            if (dateTourneeValide.Value == dateAutorisee)
            {
                // Contrôle croisé : les lignes reçues doivent exister dans les lignes préparables de la date autorisée.
                var dateOnly = DateOnly.FromDateTime(dateTourneeValide.Value);
                var lignesPreparables = (await _tourneesRepository.GetTourneeLinesAsync(dateOnly, codeLivreur: string.Empty, codeTournee: null)).ToList();
                idLignesPreparables = lignesPreparables
                    .Select(ligne => TourneeMobileMapper.BuildIdLigneSource(dateOnly, ligne))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        ValidateTournees(request, idLignesPreparables, errors, dateVerrouillageDemandeeValide);

        return errors;
    }

    /// <summary>
    /// Valide les tournées, les lignes et les quantités prévues contenues dans le lot Expédition.
    /// </summary>
    /// <remarks>
    /// Les identifiants de lignes doivent être uniques dans une tournée et dans le lot global.
    /// Les quantités prévues restent optionnelles, mais chaque quantité transmise doit porter
    /// un article autorisé et une valeur positive ou nulle.
    /// </remarks>
    private void ValidateTournees(
        ExpeditionVerrouillageLotRequest request,
        HashSet<string>? idLignesPreparables,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee)
    {
        var idLignesGlobal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var indexTournee = 0; indexTournee < request.Tournees.Count; indexTournee++)
        {
            var tournee = request.Tournees[indexTournee];
            var prefixTournee = $"tournees[{indexTournee}]";

            if (string.IsNullOrWhiteSpace(tournee.CodeTournee))
            {
                errors.Add($"{prefixTournee}.codeTournee est obligatoire.");
            }

            if (!string.IsNullOrWhiteSpace(tournee.StatutPreparationWeb) &&
                !StatutsPreparationWebAutorises.Contains(tournee.StatutPreparationWeb))
            {
                errors.Add($"{prefixTournee}.statutPreparationWeb doit être PRETE_VERROUILLAGE ou EN_PREPARATION_WEB.");
            }

            ValidateDateModificationTournee(tournee, prefixTournee, errors, dateVerrouillageDemandee);

            if (tournee.Lignes is null || tournee.Lignes.Count == 0)
            {
                errors.Add($"{prefixTournee}.lignes doit contenir au moins une ligne.");
                continue;
            }

            var idLignesTournee = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var indexLigne = 0; indexLigne < tournee.Lignes.Count; indexLigne++)
            {
                var ligne = tournee.Lignes[indexLigne];
                var prefixLigne = $"{prefixTournee}.lignes[{indexLigne}]";

                if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
                {
                    errors.Add($"{prefixLigne}.idLigneSource est obligatoire.");
                }
                else
                {
                    var idLigne = ligne.IdLigneSource.Trim();
                    if (!idLignesTournee.Add(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource est présent plusieurs fois dans la même tournée.");
                    }
                    if (!idLignesGlobal.Add(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource est présent dans plusieurs tournées du lot.");
                    }
                    if (idLignesPreparables is not null && !idLignesPreparables.Contains(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource n'existe pas dans les lignes préparables de la date.");
                    }
                }

                if (ligne.Client is null || string.IsNullOrWhiteSpace(ligne.Client.NumClient))
                {
                    errors.Add($"{prefixLigne}.client.numClient est obligatoire.");
                }

                if (ligne.QuantitesPrevues is null)
                {
                    continue;
                }

                var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var indexQuantite = 0; indexQuantite < ligne.QuantitesPrevues.Count; indexQuantite++)
                {
                    var quantite = ligne.QuantitesPrevues[indexQuantite];
                    var prefixQuantite = $"{prefixLigne}.quantitesPrevues[{indexQuantite}]";

                    if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle est obligatoire.");
                        continue;
                    }
                    var codeArticle = NormalizeArticleCode(quantite.CodeArticle);
                    if (!codesArticles.Add(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle est présent plusieurs fois dans la même ligne.");
                    }
                    if (!ArticlesAutorises.Contains(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle doit être ROLLS, ROLLS_VIDES, TAPIS ou SACS.");
                    }
                    if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value < 0)
                    {
                        errors.Add($"{prefixQuantite}.quantiteLivreePrevue doit être positive ou nulle.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Valide la date de modification fonctionnelle envoyée par ServeWeb pour une tournée.
    /// </summary>
    /// <remarks>
    /// Cette date représente l'heure du clic ou de la dernière modification côté Expédition.
    /// Elle doit porter un offset explicite et ne doit pas être postérieure au verrouillage demandé.
    /// </remarks>
    private static void ValidateDateModificationTournee(
        ExpeditionVerrouillageTourneeRequest tournee,
        string prefixTournee,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee)
    {
        if (string.IsNullOrWhiteSpace(tournee.DateModification))
        {
            return;
        }
        if (!DateTimeOffset.TryParse(tournee.DateModification, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateModification))
        {
            errors.Add($"{prefixTournee}.dateModification doit être une date ISO 8601 valide avec offset.");
            return;
        }
        if (!HasExplicitOffset(tournee.DateModification))
        {
            errors.Add($"{prefixTournee}.dateModification doit contenir un offset horaire explicite.");
            return;
        }
        if (dateVerrouillageDemandee.HasValue && dateModification > dateVerrouillageDemandee.Value.AddMinutes(5))
        {
            errors.Add($"{prefixTournee}.dateModification ne peut pas être postérieure à dateVerrouillageDemandee.");
        }
    }

    /// <summary>
    /// Normalise un code article avant contrôle d'autorisation.
    /// </summary>
    private static string NormalizeArticleCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Vérifie qu'une date textuelle porte explicitement un offset horaire.
    /// </summary>
    /// <remarks>
    /// Le verrouillage Expédition repose sur le fuseau métier Europe/Paris. Exiger un offset
    /// évite d'interpréter une date locale ambiguë différemment selon l'environnement serveur.
    /// </remarks>
    private static bool HasExplicitOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var trimmed = value.Trim();
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return trimmed.Length >= 6
            && (trimmed[^6] == '+' || trimmed[^6] == '-')
            && char.IsDigit(trimmed[^5])
            && char.IsDigit(trimmed[^4])
            && trimmed[^3] == ':'
            && char.IsDigit(trimmed[^2])
            && char.IsDigit(trimmed[^1]);
    }
}
