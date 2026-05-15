using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_ASP.NET_Core.Services;

public sealed class ExpeditionService
{
    private const string SchemaVersionExpedition = "1.0";

    private readonly TourneesRepository _tourneesRepository;
    private readonly ExpeditionRepository _expeditionRepository;
    private readonly ILogger<ExpeditionService> _logger;

    public ExpeditionService(
        TourneesRepository tourneesRepository,
        ExpeditionRepository expeditionRepository,
        ILogger<ExpeditionService> logger)
    {
        _tourneesRepository = tourneesRepository;
        _expeditionRepository = expeditionRepository;
        _logger = logger;
    }

    public async Task<ExpeditionPreparationResponseDto> GetPreparationsAPreparerAsync(
        CancellationToken cancellationToken = default)
    {
        var dateTournee = await GetProchaineDatePreparableAsync(cancellationToken);

        return await GetPreparationsAPreparerAsync(
            dateTournee,
            cancellationToken);
    }

    public async Task<ExpeditionPreparationResponseDto> GetPreparationsAPreparerAsync(
        DateOnly dateTournee,
        CancellationToken cancellationToken = default)
    {
        var lignes = (await _tourneesRepository.GetTourneeLinesAsync(
            dateTournee,
            codeLivreur: string.Empty,
            codeTournee: null)).ToList();

        var articles = (await _tourneesRepository.GetArticlesSaisissablesAsync())
            .Where(article => !IsRollsVides(article.CodeArticle))
            .OrderBy(article => article.OrdreAffichage)
            .ThenBy(article => article.CodeArticle)
            .ToList();

        if (articles.Count == 0)
        {
            articles = ArticlesSaisissables.ActifsV1
                .Where(article => !IsRollsVides(article.CodeArticle))
                .Select((article, index) => new ArticleSaisissableRecord
                {
                    CodeArticle = article.CodeArticle,
                    LibelleArticle = article.Libelle,
                    OrdreAffichage = index + 1
                })
                .ToList();
        }

        var lignesParTournee = lignes
            .GroupBy(ligne => ligne.CodeTournee, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                groupe => groupe.Key,
                groupe => groupe.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var preRemplissagesParTournee = new Dictionary<string, List<PreRemplissageQuantiteRecord>>(StringComparer.OrdinalIgnoreCase);
        var commentairesParTournee = new Dictionary<string, List<CommentaireExceptionnelRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var codeTournee in lignesParTournee.Keys)
        {
            if (string.IsNullOrWhiteSpace(codeTournee))
            {
                continue;
            }

            preRemplissagesParTournee[codeTournee] = (await _tourneesRepository.GetPreRemplissagesAsync(
                dateTournee,
                codeTournee)).ToList();

            commentairesParTournee[codeTournee] = (await _tourneesRepository.GetCommentairesExceptionnelsAsync(
                dateTournee,
                codeTournee)).ToList();
        }

        var lignesDto = lignes.Select(ligne =>
        {
            var idLigneSource = TourneeMobileMapper.BuildIdLigneSource(dateTournee, ligne);

            var preRemplissages = string.IsNullOrWhiteSpace(ligne.CodeTournee)
                ? new List<PreRemplissageQuantiteRecord>()
                : preRemplissagesParTournee.GetValueOrDefault(ligne.CodeTournee, new List<PreRemplissageQuantiteRecord>());

            var commentaires = string.IsNullOrWhiteSpace(ligne.CodeTournee)
                ? new List<CommentaireExceptionnelRecord>()
                : commentairesParTournee.GetValueOrDefault(ligne.CodeTournee, new List<CommentaireExceptionnelRecord>());

            return new ExpeditionPreparationLigneDto
            {
                IdLigneSource = idLigneSource,
                OrdreArret = ligne.OrdreArret,
                Horaire = ligne.Horaire?.ToString(),
                NumClient = ligne.NumClient,
                NomClient = ligne.NomClient,
                NomAffiche = ligne.NomAffiche,
                CodePDL = ligne.CodePDL,
                DescriptionPDL = ligne.DescriptionPDL,
                CodeTournee = ligne.CodeTournee,
                LibelleTournee = ligne.LibelleTournee,
                CommentaireExceptionnel = FindCommentaireExceptionnel(idLigneSource, ligne, commentaires),
                Quantites = articles.Select(article =>
                {
                    var preRemplissage = FindPreRemplissage(
                        idLigneSource,
                        ligne,
                        article.CodeArticle,
                        preRemplissages);

                    return new ExpeditionPreparationQuantiteDto
                    {
                        CodeArticle = article.CodeArticle.Trim().ToUpperInvariant(),
                        Libelle = preRemplissage?.LibelleArticle ?? article.LibelleArticle,
                        QuantiteLivreePrevue = preRemplissage?.QuantiteLivreePrevue
                    };
                }).ToList()
            };
        }).ToList();

        return new ExpeditionPreparationResponseDto
        {
            SchemaVersion = SchemaVersionExpedition,
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            CodeTournee = null,
            NombreLignes = lignesDto.Count,
            Lignes = lignesDto
        };
    }

    public async Task<(int StatusCode, ExpeditionApiResult Body)> VerrouillerPreparationAsync(
        ExpeditionVerrouillageRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateVerrouillageRequest(request, out var dateTournee, out var idLotVerrouillage);

        if (errors.Count > 0 || request is null)
        {
            return (
                StatusCodes.Status400BadRequest,
                new ExpeditionApiResult
                {
                    Statut = "VALIDATION_ERROR",
                    Message = "La préparation Expédition contient des données invalides.",
                    Errors = errors
                });
        }

        var empreintePayload = ComputePayloadFingerprint(request, dateTournee, idLotVerrouillage);
        var existingLot = await _expeditionRepository.GetLotVerrouillageAsync(
            idLotVerrouillage,
            cancellationToken);

        if (existingLot is not null)
        {
            if (string.Equals(existingLot.EmpreintePayload, empreintePayload, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    StatusCodes.Status200OK,
                    new ExpeditionApiResult
                    {
                        Statut = "SUCCESS",
                        Code = "EXPEDITION_LOT_ALREADY_LOCKED",
                        Message = "Préparation Expédition déjà verrouillée avec le même contenu.",
                        IdPreRemplissageTournee = existingLot.IdPreRemplissageTournee,
                        IdLotVerrouillage = existingLot.IdLotVerrouillage.ToString("D")
                    });
            }

            return (
                StatusCodes.Status409Conflict,
                new ExpeditionApiResult
                {
                    Statut = "CONFLICT",
                    Code = "EXPEDITION_LOT_PAYLOAD_MISMATCH",
                    Message = "Ce lot de verrouillage a déjà été reçu avec un contenu différent. Le rejeu est refusé."
                });
        }

        var preparationExistante = await _expeditionRepository.GetPreparationEtatAsync(
            dateTournee,
            request.CodeTournee,
            cancellationToken);

        if (preparationExistante is not null &&
            preparationExistante.EstVerrouille &&
            preparationExistante.IdLotVerrouillage.HasValue &&
            preparationExistante.IdLotVerrouillage.Value != idLotVerrouillage)
        {
            return (
                StatusCodes.Status409Conflict,
                new ExpeditionApiResult
                {
                    Statut = "CONFLICT",
                    Code = "EXPEDITION_PREPARATION_ALREADY_LOCKED",
                    Message = "Cette préparation Expédition est déjà verrouillée pour cette date et cette tournée.",
                    IdPreRemplissageTournee = preparationExistante.IdPreRemplissageTournee,
                    IdLotVerrouillage = preparationExistante.IdLotVerrouillage?.ToString("D")
                });
        }

        try
        {
            var idPreRemplissageTournee = await _expeditionRepository.EnregistrerVerrouillageAsync(
                request,
                dateTournee,
                idLotVerrouillage,
                empreintePayload,
                adresseIp,
                cancellationToken);

            return (
                StatusCodes.Status200OK,
                new ExpeditionApiResult
                {
                    Statut = "SUCCESS",
                    Code = "EXPEDITION_PREPARATION_LOCKED",
                    Message = "Préparation Expédition verrouillée avec succès.",
                    IdPreRemplissageTournee = idPreRemplissageTournee,
                    IdLotVerrouillage = idLotVerrouillage.ToString("D")
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erreur lors du verrouillage Expédition pour la tournée {CodeTournee} du {DateTournee}.",
                request.CodeTournee,
                dateTournee);

            return (
                StatusCodes.Status500InternalServerError,
                new ExpeditionApiResult
                {
                    Statut = "ERROR",
                    Code = "SERVER_ERROR",
                    Message = "Une erreur technique est survenue pendant le verrouillage Expédition."
                });
        }
    }

    private async Task<DateOnly> GetProchaineDatePreparableAsync(CancellationToken cancellationToken = default)
    {
        var dateCourante = DateOnly.FromDateTime(DateTime.Today);

        for (var indexJour = 0; indexJour <= 14; indexJour++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dateCandidate = dateCourante.AddDays(indexJour);
            var lignes = await _tourneesRepository.GetTourneeLinesAsync(
                dateCandidate,
                codeLivreur: string.Empty,
                codeTournee: null);

            if (lignes.Any())
            {
                return dateCandidate;
            }
        }

        throw new InvalidOperationException("Aucune tournée à préparer n'a été trouvée sur les 14 prochains jours.");
    }

    private static List<string> ValidateVerrouillageRequest(
        ExpeditionVerrouillageRequest? request,
        out DateTime dateTournee,
        out Guid idLotVerrouillage)
    {
        var errors = new List<string>();
        dateTournee = default;
        idLotVerrouillage = default;

        if (request is null)
        {
            errors.Add("Le corps JSON de la préparation Expédition est obligatoire.");
            return errors;
        }

        if (!string.IsNullOrWhiteSpace(request.SchemaVersion) &&
            !string.Equals(request.SchemaVersion.Trim(), SchemaVersionExpedition, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"La version de schéma Expédition supportée est {SchemaVersionExpedition}.");
        }

        if (!Guid.TryParse(request.IdLotVerrouillage, out idLotVerrouillage))
        {
            errors.Add("idLotVerrouillage est obligatoire et doit être un GUID valide.");
        }

        if (!DateTime.TryParse(request.DateTournee, out dateTournee))
        {
            errors.Add("dateTournee est obligatoire et doit être une date valide au format yyyy-MM-dd.");
        }
        else
        {
            dateTournee = dateTournee.Date;
        }

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
        {
            errors.Add("codeTournee est obligatoire.");
        }

        if (request.Lignes is null || request.Lignes.Count == 0)
        {
            errors.Add("La préparation Expédition doit contenir au moins une ligne.");
            return errors;
        }

        var idLignes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var indexLigne = 0; indexLigne < request.Lignes.Count; indexLigne++)
        {
            var ligne = request.Lignes[indexLigne];
            var prefix = $"lignes[{indexLigne}]";

            if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
            {
                errors.Add($"{prefix}.idLigneSource est obligatoire.");
            }
            else if (!idLignes.Add(ligne.IdLigneSource.Trim()))
            {
                errors.Add($"{prefix}.idLigneSource est présent plusieurs fois dans le lot.");
            }

            if (string.IsNullOrWhiteSpace(ligne.NumClient))
            {
                errors.Add($"{prefix}.numClient est obligatoire.");
            }

            if (ligne.Quantites is null)
            {
                continue;
            }

            var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var indexQuantite = 0; indexQuantite < ligne.Quantites.Count; indexQuantite++)
            {
                var quantite = ligne.Quantites[indexQuantite];
                var quantitePrefix = $"{prefix}.quantites[{indexQuantite}]";

                if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
                {
                    errors.Add($"{quantitePrefix}.codeArticle est obligatoire.");
                    continue;
                }

                var codeArticle = quantite.CodeArticle.Trim();

                if (!codesArticles.Add(codeArticle))
                {
                    errors.Add($"{quantitePrefix}.codeArticle est présent plusieurs fois dans la même ligne.");
                }

                if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value < 0)
                {
                    errors.Add($"{quantitePrefix}.quantiteLivreePrevue doit être positive ou nulle.");
                }

                if (IsRollsVides(codeArticle) &&
                    quantite.QuantiteLivreePrevue.HasValue &&
                    quantite.QuantiteLivreePrevue.Value > 0)
                {
                    errors.Add("L'article ROLLS_VIDES ne peut pas être préparé côté Expédition car il est uniquement récupéré.");
                }
            }
        }

        return errors;
    }

    private static string ComputePayloadFingerprint(
        ExpeditionVerrouillageRequest request,
        DateTime dateTournee,
        Guid idLotVerrouillage)
    {
        var normalizedPayload = new
        {
            schemaVersion = string.IsNullOrWhiteSpace(request.SchemaVersion)
                ? SchemaVersionExpedition
                : request.SchemaVersion.Trim(),
            idLotVerrouillage = idLotVerrouillage.ToString("D"),
            dateTournee = dateTournee.ToString("yyyy-MM-dd"),
            codeTournee = request.CodeTournee.Trim(),
            libelleTournee = NormalizeNullable(request.LibelleTournee),
            lignes = request.Lignes
                .OrderBy(ligne => ligne.IdLigneSource, StringComparer.OrdinalIgnoreCase)
                .Select(ligne => new
                {
                    idLigneSource = ligne.IdLigneSource.Trim(),
                    ordreArret = ligne.OrdreArret,
                    numClient = ligne.NumClient.Trim(),
                    codePDL = NormalizeNullable(ligne.CodePDL),
                    commentaireExceptionnel = NormalizeNullable(ligne.CommentaireExceptionnel),
                    quantites = (ligne.Quantites ?? new List<ExpeditionVerrouillageQuantiteRequest>())
                        .OrderBy(quantite => quantite.CodeArticle, StringComparer.OrdinalIgnoreCase)
                        .Select(quantite => new
                        {
                            codeArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                            quantiteLivreePrevue = quantite.QuantiteLivreePrevue
                        })
                        .ToList()
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(
            normalizedPayload,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                WriteIndented = false
            });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static PreRemplissageQuantiteRecord? FindPreRemplissage(
        string idLigneSource,
        TourneeLigneRecord ligne,
        string codeArticle,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages)
    {
        var byIdLigneSource = preRemplissages.FirstOrDefault(preRemplissage =>
            string.Equals(preRemplissage.IdLigneSource, idLigneSource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(preRemplissage.CodeArticle, codeArticle, StringComparison.OrdinalIgnoreCase));

        if (byIdLigneSource is not null)
        {
            return byIdLigneSource;
        }

        return preRemplissages.FirstOrDefault(preRemplissage =>
            string.Equals(preRemplissage.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIdPart(preRemplissage.CodePDL), NormalizeIdPart(ligne.CodePDL), StringComparison.OrdinalIgnoreCase)
            && string.Equals(preRemplissage.CodeArticle, codeArticle, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindCommentaireExceptionnel(
        string idLigneSource,
        TourneeLigneRecord ligne,
        IReadOnlyList<CommentaireExceptionnelRecord> commentaires)
    {
        var commentaireLigne = commentaires.FirstOrDefault(commentaire =>
            !string.IsNullOrWhiteSpace(commentaire.IdLigneSource)
            && string.Equals(commentaire.IdLigneSource, idLigneSource, StringComparison.OrdinalIgnoreCase));

        if (commentaireLigne is not null)
        {
            return NormalizeNullable(commentaireLigne.Commentaire);
        }

        var commentaireExact = commentaires.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIdPart(commentaire.CodePDL), NormalizeIdPart(ligne.CodePDL), StringComparison.OrdinalIgnoreCase));

        if (commentaireExact is not null)
        {
            return NormalizeNullable(commentaireExact.Commentaire);
        }

        var commentaireClient = commentaires.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(commentaire.CodePDL));

        return NormalizeNullable(commentaireClient?.Commentaire);
    }

    private static bool IsRollsVides(string? codeArticle)
    {
        return string.Equals(codeArticle?.Trim(), ArticlesSaisissables.RollsVides, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIdPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "NA"
            : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
