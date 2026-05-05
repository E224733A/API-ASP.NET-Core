using Dapper;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Repositories;

/// <summary>
/// Repository pour l'enregistrement, la vérification et la consultation des synchronisations.
///
/// Tables utilisées :
/// - Mobile_Livreur
/// - Mobile_Tournee
/// - Mobile_TourneeLigne
/// - Mobile_TourneeLigneQuantite
/// - Mobile_LogSynchronisation
///
/// Contrat JSON officiel :
/// - schemaVersion = 1.1
/// - saisie.quantites[]
/// - quantiteLivree
/// - quantiteRecuperee
/// </summary>
public sealed class SynchronisationsRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SynchronisationsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Vérifie si une synchronisation existe déjà dans Mobile_Tournee.
    /// Anti-doublon technique : un même paquet mobile ne doit pas être enregistré deux fois.
    /// </summary>
    public async Task<bool> SynchronisationExistsAsync(string idSynchronisation)
    {
        if (!Guid.TryParse(idSynchronisation, out var guid))
        {
            return false;
        }

        using var connection = _connectionFactory.CreateMobileConnection();

        var exists = await connection.QueryFirstOrDefaultAsync<int?>(
            """
            SELECT 1
            FROM Mobile_Tournee
            WHERE IdSynchronisation = @IdSynchronisation;
            """,
            new
            {
                IdSynchronisation = guid
            });

        return exists.HasValue;
    }

    /// <summary>
    /// Consulte les synchronisations envoyées.
    ///
    /// Filtres possibles :
    /// - dateTournee
    /// - codeTournee
    /// - codeLivreur
    /// </summary>
    public async Task<List<SynchronisationResumeDto>> GetSynchronisationsAsync(
        DateTime? dateTournee,
        string? codeTournee,
        string? codeLivreur)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var result = await connection.QueryAsync<SynchronisationResumeDto>(
            """
            SELECT
                t.IdTourneeMobile,
                t.IdSynchronisation,
                t.DateTournee,
                t.CodeTournee,
                t.LibelleTournee,
                t.IdLivreur,
                l.CodeLivreur,
                l.NomLivreur,
                t.StatutSynchronisation,
                t.DateChargementMobile,
                t.DateReceptionApi,
                t.DateEnvoi,
                t.EstVerrouillee,
                t.NombrePointsPrevus,
                t.NombrePointsSaisis,
                t.CommentaireGlobal,
                t.NomAppareil,
                t.VersionApplication,
                SUM(CASE WHEN tl.StatutPassage = @StatutFait THEN 1 ELSE 0 END) AS NombreFaits,
                SUM(CASE WHEN tl.StatutPassage = @StatutNonFait THEN 1 ELSE 0 END) AS NombreNonFaits,
                SUM(CASE WHEN tl.StatutPassage = @StatutAnomalie THEN 1 ELSE 0 END) AS NombreAnomalies
            FROM Mobile_Tournee t
            LEFT JOIN Mobile_Livreur l
                ON l.IdLivreur = t.IdLivreur
            LEFT JOIN Mobile_TourneeLigne tl
                ON tl.IdTourneeMobile = t.IdTourneeMobile
            WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
              AND (@CodeTournee IS NULL OR t.CodeTournee = @CodeTournee)
              AND (@CodeLivreur IS NULL OR l.CodeLivreur = @CodeLivreur)
            GROUP BY
                t.IdTourneeMobile,
                t.IdSynchronisation,
                t.DateTournee,
                t.CodeTournee,
                t.LibelleTournee,
                t.IdLivreur,
                l.CodeLivreur,
                l.NomLivreur,
                t.StatutSynchronisation,
                t.DateChargementMobile,
                t.DateReceptionApi,
                t.DateEnvoi,
                t.EstVerrouillee,
                t.NombrePointsPrevus,
                t.NombrePointsSaisis,
                t.CommentaireGlobal,
                t.NomAppareil,
                t.VersionApplication
            ORDER BY
                t.DateReceptionApi DESC,
                t.IdTourneeMobile DESC;
            """,
            new
            {
                DateTournee = dateTournee?.Date,
                CodeTournee = EmptyToNull(codeTournee),
                CodeLivreur = EmptyToNull(codeLivreur),
                StatutFait = StatutsPassage.Fait,
                StatutNonFait = StatutsPassage.NonFait,
                StatutAnomalie = StatutsPassage.Anomalie
            });

        return result.ToList();
    }

    /// <summary>
    /// Consulte le détail complet d'une synchronisation envoyée.
    ///
    /// Inclut :
    /// - l'en-tête de tournée ;
    /// - les lignes saisies ;
    /// - les quantités détaillées ;
    /// - les logs associés.
    /// </summary>
    public async Task<SynchronisationDetailDto?> GetSynchronisationByIdAsync(long idTourneeMobile)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var entete = await connection.QuerySingleOrDefaultAsync<SynchronisationResumeDto>(
            """
            SELECT
                t.IdTourneeMobile,
                t.IdSynchronisation,
                t.DateTournee,
                t.CodeTournee,
                t.LibelleTournee,
                t.IdLivreur,
                l.CodeLivreur,
                l.NomLivreur,
                t.StatutSynchronisation,
                t.DateChargementMobile,
                t.DateReceptionApi,
                t.DateEnvoi,
                t.EstVerrouillee,
                t.NombrePointsPrevus,
                t.NombrePointsSaisis,
                t.CommentaireGlobal,
                t.NomAppareil,
                t.VersionApplication,
                (
                    SELECT COUNT(1)
                    FROM Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutFait
                ) AS NombreFaits,
                (
                    SELECT COUNT(1)
                    FROM Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutNonFait
                ) AS NombreNonFaits,
                (
                    SELECT COUNT(1)
                    FROM Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutAnomalie
                ) AS NombreAnomalies
            FROM Mobile_Tournee t
            LEFT JOIN Mobile_Livreur l
                ON l.IdLivreur = t.IdLivreur
            WHERE t.IdTourneeMobile = @IdTourneeMobile;
            """,
            new
            {
                IdTourneeMobile = idTourneeMobile,
                StatutFait = StatutsPassage.Fait,
                StatutNonFait = StatutsPassage.NonFait,
                StatutAnomalie = StatutsPassage.Anomalie
            });

        if (entete is null)
        {
            return null;
        }

        var lignes = await connection.QueryAsync<SynchronisationLigneDetailDto>(
            """
            SELECT
                IdTourneeLigne,
                IdTourneeMobile,
                IdLigneSource,
                OrdreArret,
                NumClient,
                NomClient,
                NomAffiche,
                CodePDL,
                DescriptionPDL,
                AdresseLigne1,
                AdresseLigne2,
                AdresseLigne3,
                Ville,
                CodePostal,
                JourTournee,
                SchemaLivraison,
                CodeTournee,
                LibelleTournee,
                JourTourneeRetour,
                CodeTourneeRetour,
                LibelleTourneeRetour,
                Instructions,
                ZoneDechargement,
                Zone,
                TypeLinge,
                EstFerme,
                DateFermeture,
                MotifFermeture,
                QuantiteLivree,
                QuantiteReprise,
                PrecisionLivreur,
                StatutPassage,
                CommentaireLivreur,
                HeureValidation,
                EstValidee,
                DateCreation,
                DateModification
            FROM Mobile_TourneeLigne
            WHERE IdTourneeMobile = @IdTourneeMobile
            ORDER BY
                OrdreArret,
                IdTourneeLigne;
            """,
            new
            {
                IdTourneeMobile = idTourneeMobile
            });

        var quantites = await connection.QueryAsync<SynchronisationQuantiteDetailDto>(
            """
            SELECT
                q.IdQuantite,
                q.IdTourneeLigne,
                q.CodeArticle,
                q.LibelleArticle,
                q.QuantiteLivree,
                q.QuantiteRecuperee,
                q.DateCreation,
                q.DateModification
            FROM Mobile_TourneeLigneQuantite q
            INNER JOIN Mobile_TourneeLigne l
                ON l.IdTourneeLigne = q.IdTourneeLigne
            WHERE l.IdTourneeMobile = @IdTourneeMobile
            ORDER BY
                l.OrdreArret,
                q.CodeArticle;
            """,
            new
            {
                IdTourneeMobile = idTourneeMobile
            });

        var logs = await connection.QueryAsync<SynchronisationLogDto>(
            """
            SELECT
                IdLog,
                IdTourneeMobile,
                IdLivreur,
                IdSynchronisation,
                DateEvenement,
                TypeEvenement,
                Niveau,
                Message,
                DetailTechnique,
                AdresseIP,
                NomAppareil,
                VersionApplication
            FROM Mobile_LogSynchronisation
            WHERE IdTourneeMobile = @IdTourneeMobile
            ORDER BY
                DateEvenement DESC,
                IdLog DESC;
            """,
            new
            {
                IdTourneeMobile = idTourneeMobile
            });

        return new SynchronisationDetailDto
        {
            Entete = entete,
            Lignes = lignes.ToList(),
            Quantites = quantites.ToList(),
            Logs = logs.ToList()
        };
    }

    /// <summary>
    /// Enregistre une synchronisation de tournée complète.
    ///
    /// Le repository lit uniquement :
    /// - saisie.quantites[]
    /// - quantiteLivree
    /// - quantiteRecuperee
    ///
    /// Les totaux de compatibilité QuantiteLivree et QuantiteReprise
    /// sont recalculés depuis le tableau des quantités.
    /// </summary>
    public async Task SaveSynchronisationAsync(SynchronisationTourneeRequest request)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var now = DateTime.Now;

            var idSynchronisationGuid = ParseRequiredGuid(
                request.IdSynchronisation,
                "IdSynchronisation doit être un GUID valide.");

            var dateTournee = ParseRequiredDate(
                request.DateTournee,
                "DateTournee invalide.");

            var codeLivreur = RequiredTrimmed(
                request.Livreur?.CodeLivreur,
                "Livreur.CodeLivreur est obligatoire.");

            var nomLivreur = string.IsNullOrWhiteSpace(request.Livreur?.NomLivreur)
                ? codeLivreur
                : request.Livreur.NomLivreur.Trim();

            var codeTournee = RequiredTrimmed(
                request.CodeTournee,
                "CodeTournee est obligatoire.");

            var dateChargementMobile = ParseOptionalDateTime(request.Mobile?.DateChargementMobile);
            var dateEnvoiMobile = ParseOptionalDateTime(request.Mobile?.DateEnvoiMobile);

            await connection.ExecuteAsync(
                """
                MERGE INTO Mobile_Livreur AS target
                USING (
                    SELECT
                        @CodeLivreur AS CodeLivreur,
                        @NomLivreur AS NomLivreur
                ) AS source
                ON target.CodeLivreur = source.CodeLivreur
                WHEN MATCHED THEN
                    UPDATE SET
                        NomLivreur = source.NomLivreur,
                        EstActif = 1,
                        DateModification = @Now
                WHEN NOT MATCHED THEN
                    INSERT (
                        CodeLivreur,
                        NomLivreur,
                        EstActif,
                        DateCreation,
                        DateModification
                    )
                    VALUES (
                        source.CodeLivreur,
                        source.NomLivreur,
                        1,
                        @Now,
                        NULL
                    );
                """,
                new
                {
                    CodeLivreur = codeLivreur,
                    NomLivreur = nomLivreur,
                    Now = now
                },
                transaction);

            var idLivreur = await connection.QuerySingleAsync<int>(
                """
                SELECT IdLivreur
                FROM Mobile_Livreur
                WHERE CodeLivreur = @CodeLivreur;
                """,
                new
                {
                    CodeLivreur = codeLivreur
                },
                transaction);

            var idTourneeMobile = await connection.QuerySingleAsync<long>(
                """
                INSERT INTO Mobile_Tournee (
                    IdSynchronisation,
                    DateTournee,
                    CodeTournee,
                    LibelleTournee,
                    IdLivreur,
                    StatutSynchronisation,
                    DateChargementMobile,
                    DateReceptionApi,
                    DateEnvoi,
                    EstVerrouillee,
                    NombrePointsPrevus,
                    NombrePointsSaisis,
                    CommentaireGlobal,
                    NomAppareil,
                    VersionApplication,
                    AdresseIP,
                    DateCreation,
                    DateModification
                )
                OUTPUT INSERTED.IdTourneeMobile
                VALUES (
                    @IdSynchronisation,
                    @DateTournee,
                    @CodeTournee,
                    @LibelleTournee,
                    @IdLivreur,
                    @StatutSynchronisation,
                    @DateChargementMobile,
                    @DateReceptionApi,
                    @DateEnvoi,
                    1,
                    @NombrePointsPrevus,
                    @NombrePointsSaisis,
                    @CommentaireGlobal,
                    @NomAppareil,
                    @VersionApplication,
                    NULL,
                    @Now,
                    NULL
                );
                """,
                new
                {
                    IdSynchronisation = idSynchronisationGuid,
                    DateTournee = dateTournee.Date,
                    CodeTournee = codeTournee,
                    LibelleTournee = request.LibelleTournee,
                    IdLivreur = idLivreur,
                    StatutSynchronisation = StatutsSynchronisation.Envoyee,
                    DateChargementMobile = dateChargementMobile,
                    DateReceptionApi = now,
                    DateEnvoi = dateEnvoiMobile ?? now,
                    NombrePointsPrevus = request.Lignes.Count,
                    NombrePointsSaisis = request.Lignes.Count,
                    CommentaireGlobal = request.CommentaireGlobal,
                    NomAppareil = request.Mobile?.NomAppareil,
                    VersionApplication = request.Mobile?.VersionApplication,
                    Now = now
                },
                transaction);

            foreach (var ligne in request.Lignes)
            {
                if (ligne.Saisie is null)
                {
                    throw new ArgumentException("Chaque ligne doit contenir une saisie.");
                }

                var quantites = NormalizeQuantites(ligne.Saisie.Quantites);

                if (quantites.Count == 0)
                {
                    throw new ArgumentException("Chaque ligne doit contenir au moins une quantité.");
                }

                var quantiteLivree = quantites.Sum(q => q.QuantiteLivree);
                var quantiteReprise = quantites.Sum(q => q.QuantiteRecuperee);

                var nbExpes = GetQuantiteLivreePourArticle(quantites, ArticlesSaisissables.Expes);
                var nbRolls = GetQuantiteLivreePourArticle(quantites, ArticlesSaisissables.Rolls);
                var nbVetements = GetQuantiteLivreePourArticle(quantites, ArticlesSaisissables.Vetements);
                var nbTapis = GetQuantiteLivreePourArticle(quantites, ArticlesSaisissables.Tapis);
                var nbSacs = GetQuantiteLivreePourArticle(quantites, ArticlesSaisissables.Sacs);

                /*
                 * NbRecuperes reste une colonne de compatibilité.
                 * Le détail officiel est stocké dans Mobile_TourneeLigneQuantite.
                 */
                var nbRecuperes = quantiteReprise;

                var heureValidation = ParseHeureValidation(
                    ligne.Saisie.HeureValidation,
                    dateTournee.Date);

                var statutPassage = RequiredTrimmed(
                    ligne.Saisie.StatutPassage,
                    "StatutPassage est obligatoire.").ToUpperInvariant();

                var idLigneSource = RequiredTrimmed(
                    ligne.IdLigneSource,
                    "IdLigneSource est obligatoire.");

                var numClient = RequiredTrimmed(
                    ligne.Client?.NumClient,
                    "Client.NumClient est obligatoire.");

                var nomClient = RequiredTrimmed(
                    ligne.Client?.NomClient,
                    "Client.NomClient est obligatoire.");

                var idTourneeLigne = await connection.QuerySingleAsync<long>(
                    """
                    INSERT INTO Mobile_TourneeLigne (
                        IdTourneeMobile,
                        IdLigneSource,
                        CodeTournee,
                        OrdreArret,
                        NumClient,
                        NomClient,
                        NomAffiche,
                        CodePDL,
                        DescriptionPDL,
                        NbExpes,
                        NbRolls,
                        NbVetements,
                        NbTapis,
                        NbSacs,
                        NbRecuperes,
                        QuantiteLivree,
                        QuantiteReprise,
                        PrecisionLivreur,
                        StatutPassage,
                        CommentaireLivreur,
                        HeureValidation,
                        EstValidee,
                        DateCreation,
                        DateModification
                    )
                    OUTPUT INSERTED.IdTourneeLigne
                    VALUES (
                        @IdTourneeMobile,
                        @IdLigneSource,
                        @CodeTournee,
                        @OrdreArret,
                        @NumClient,
                        @NomClient,
                        @NomAffiche,
                        @CodePDL,
                        @DescriptionPDL,
                        @NbExpes,
                        @NbRolls,
                        @NbVetements,
                        @NbTapis,
                        @NbSacs,
                        @NbRecuperes,
                        @QuantiteLivree,
                        @QuantiteReprise,
                        @PrecisionLivreur,
                        @StatutPassage,
                        @CommentaireLivreur,
                        @HeureValidation,
                        @EstValidee,
                        @Now,
                        NULL
                    );
                    """,
                    new
                    {
                        IdTourneeMobile = idTourneeMobile,
                        IdLigneSource = idLigneSource,
                        CodeTournee = codeTournee,
                        OrdreArret = ligne.OrdreArret,
                        NumClient = numClient,
                        NomClient = nomClient,
                        NomAffiche = EmptyToNull(ligne.Client?.NomAffiche),
                        CodePDL = EmptyToNull(ligne.PointLivraison?.CodePDL),
                        DescriptionPDL = EmptyToNull(ligne.PointLivraison?.DescriptionPDL),
                        NbExpes = nbExpes,
                        NbRolls = nbRolls,
                        NbVetements = nbVetements,
                        NbTapis = nbTapis,
                        NbSacs = nbSacs,
                        NbRecuperes = nbRecuperes,
                        QuantiteLivree = quantiteLivree,
                        QuantiteReprise = quantiteReprise,
                        PrecisionLivreur = EmptyToNull(ligne.Saisie.PrecisionLivreur),
                        StatutPassage = statutPassage,
                        CommentaireLivreur = EmptyToNull(ligne.Saisie.CommentaireLivreur),
                        HeureValidation = heureValidation,
                        EstValidee = ligne.Saisie.EstValidee,
                        Now = now
                    },
                    transaction);

                foreach (var quantite in quantites)
                {
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO Mobile_TourneeLigneQuantite (
                            IdTourneeLigne,
                            CodeArticle,
                            LibelleArticle,
                            QuantiteLivree,
                            QuantiteRecuperee,
                            DateCreation,
                            DateModification
                        )
                        VALUES (
                            @IdTourneeLigne,
                            @CodeArticle,
                            @LibelleArticle,
                            @QuantiteLivree,
                            @QuantiteRecuperee,
                            @Now,
                            NULL
                        );
                        """,
                        new
                        {
                            IdTourneeLigne = idTourneeLigne,
                            CodeArticle = quantite.CodeArticle,
                            LibelleArticle = quantite.Libelle,
                            QuantiteLivree = quantite.QuantiteLivree,
                            QuantiteRecuperee = quantite.QuantiteRecuperee,
                            Now = now
                        },
                        transaction);
                }
            }

            await connection.ExecuteAsync(
                """
                INSERT INTO Mobile_LogSynchronisation (
                    IdTourneeMobile,
                    IdLivreur,
                    IdSynchronisation,
                    DateEvenement,
                    TypeEvenement,
                    Niveau,
                    Message,
                    DetailTechnique,
                    AdresseIP,
                    NomAppareil,
                    VersionApplication
                )
                VALUES (
                    @IdTourneeMobile,
                    @IdLivreur,
                    @IdSynchronisation,
                    @DateEvenement,
                    @TypeEvenement,
                    @Niveau,
                    @Message,
                    @DetailTechnique,
                    @AdresseIP,
                    @NomAppareil,
                    @VersionApplication
                );
                """,
                new
                {
                    IdTourneeMobile = idTourneeMobile,
                    IdLivreur = idLivreur,
                    IdSynchronisation = idSynchronisationGuid,
                    DateEvenement = now,
                    TypeEvenement = "ENVOI_REUSSI",
                    Niveau = "INFO",
                    Message = "Synchronisation enregistrée avec succès.",
                    DetailTechnique = $"Tournée {codeTournee} du {dateTournee:yyyy-MM-dd} synchronisée avec {request.Lignes.Count} ligne(s).",
                    AdresseIP = (string?)null,
                    NomAppareil = request.Mobile?.NomAppareil,
                    VersionApplication = request.Mobile?.VersionApplication
                },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static Guid ParseRequiredGuid(string? value, string errorMessage)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new ArgumentException(errorMessage);
        }

        return guid;
    }

    private static DateTime ParseRequiredDate(string? value, string errorMessage)
    {
        if (!DateTime.TryParse(value, out var date))
        {
            throw new ArgumentException(errorMessage);
        }

        return date.Date;
    }

    private static DateTime? ParseOptionalDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset.DateTime;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        return null;
    }

    private static DateTime? ParseHeureValidation(string? value, DateTime dateTournee)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParse(value, out var heureSimple))
        {
            return dateTournee.Date.Add(heureSimple);
        }

        if (DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset.DateTime;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        return null;
    }

    private static string RequiredTrimmed(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorMessage);
        }

        return value.Trim();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static List<SynchronisationQuantiteRequest> NormalizeQuantites(
        List<SynchronisationQuantiteRequest>? quantites)
    {
        if (quantites is null || quantites.Count == 0)
        {
            return new List<SynchronisationQuantiteRequest>();
        }

        return quantites
            .Where(q => q is not null)
            .Select(q => new SynchronisationQuantiteRequest
            {
                CodeArticle = q.CodeArticle.Trim().ToUpperInvariant(),
                Libelle = EmptyToNull(q.Libelle),
                QuantiteLivree = q.QuantiteLivree,
                QuantiteRecuperee = q.QuantiteRecuperee
            })
            .Where(q => !string.IsNullOrWhiteSpace(q.CodeArticle))
            .ToList();
    }

    private static int GetQuantiteLivreePourArticle(
        List<SynchronisationQuantiteRequest> quantites,
        string codeArticle)
    {
        return quantites
            .Where(q => string.Equals(
                q.CodeArticle,
                codeArticle,
                StringComparison.OrdinalIgnoreCase))
            .Sum(q => q.QuantiteLivree);
    }
}