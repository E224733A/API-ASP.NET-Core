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
/// - schemaVersion = 1.2
/// - saisie.quantites[]
/// - quantiteLivreePrevue nullable
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
            FROM dbo.Mobile_Tournee
            WHERE IdSynchronisation = @IdSynchronisation;
            """,
            new
            {
                IdSynchronisation = guid
            });

        return exists.HasValue;
    }

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
                t.SchemaVersion,
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
            FROM dbo.Mobile_Tournee t
            LEFT JOIN dbo.Mobile_Livreur l
                ON l.IdLivreur = t.IdLivreur
            LEFT JOIN dbo.Mobile_TourneeLigne tl
                ON tl.IdTourneeMobile = t.IdTourneeMobile
            WHERE (@DateTournee IS NULL OR t.DateTournee = @DateTournee)
              AND (@CodeTournee IS NULL OR t.CodeTournee = @CodeTournee)
              AND (@CodeLivreur IS NULL OR l.CodeLivreur = @CodeLivreur)
            GROUP BY
                t.IdTourneeMobile,
                t.SchemaVersion,
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

    public async Task<SynchronisationDetailDto?> GetSynchronisationByIdAsync(long idTourneeMobile)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var entete = await connection.QuerySingleOrDefaultAsync<SynchronisationResumeDto>(
            """
            SELECT
                t.IdTourneeMobile,
                t.SchemaVersion,
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
                    FROM dbo.Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutFait
                ) AS NombreFaits,
                (
                    SELECT COUNT(1)
                    FROM dbo.Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutNonFait
                ) AS NombreNonFaits,
                (
                    SELECT COUNT(1)
                    FROM dbo.Mobile_TourneeLigne x
                    WHERE x.IdTourneeMobile = t.IdTourneeMobile
                      AND x.StatutPassage = @StatutAnomalie
                ) AS NombreAnomalies
            FROM dbo.Mobile_Tournee t
            LEFT JOIN dbo.Mobile_Livreur l
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
                Horaire,
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
                JourLibelle,
                SchemaLivraison,
                CodeTournee,
                LibelleTournee,
                JourTourneeRetour,
                JourRetourLibelle,
                CodeTourneeRetour,
                LibelleTourneeRetour,
                Instructions,
                CommentaireExceptionnel,
                ZoneDechargement,
                ZoneDechargementAffichee,
                Zone,
                PrecisionInfo,
                Cle,
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
            FROM dbo.Mobile_TourneeLigne
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
                q.QuantiteLivreePrevue,
                q.QuantiteLivree,
                q.QuantiteRecuperee,
                q.DateCreation,
                q.DateModification
            FROM dbo.Mobile_TourneeLigneQuantite q
            INNER JOIN dbo.Mobile_TourneeLigne l
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
            FROM dbo.Mobile_LogSynchronisation
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

    public async Task SaveSynchronisationAsync(SynchronisationTourneeRequest request)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var now = DateTimeOffset.Now;

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

            var dateChargementMobile = ParseOptionalDateTimeOffset(request.Mobile?.DateChargementMobile);
            var dateEnvoiMobile = ParseOptionalDateTimeOffset(request.Mobile?.DateEnvoiMobile);

            await connection.ExecuteAsync(
                """
                MERGE INTO dbo.Mobile_Livreur AS target
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
                FROM dbo.Mobile_Livreur
                WHERE CodeLivreur = @CodeLivreur;
                """,
                new
                {
                    CodeLivreur = codeLivreur
                },
                transaction);

            var idTourneeMobile = await connection.QuerySingleAsync<long>(
                """
                INSERT INTO dbo.Mobile_Tournee (
                    SchemaVersion,
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
                    @SchemaVersion,
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
                    SchemaVersion = RequiredTrimmed(request.SchemaVersion, "SchemaVersion est obligatoire."),
                    IdSynchronisation = idSynchronisationGuid,
                    DateTournee = dateTournee.Date,
                    CodeTournee = codeTournee,
                    LibelleTournee = EmptyToNull(request.LibelleTournee),
                    IdLivreur = idLivreur,
                    StatutSynchronisation = StatutsSynchronisation.Envoyee,
                    DateChargementMobile = dateChargementMobile,
                    DateReceptionApi = now,
                    DateEnvoi = dateEnvoiMobile ?? now,
                    NombrePointsPrevus = request.Lignes.Count,
                    NombrePointsSaisis = request.Lignes.Count,
                    CommentaireGlobal = EmptyToNull(request.CommentaireGlobal),
                    NomAppareil = EmptyToNull(request.Mobile?.NomAppareil),
                    VersionApplication = EmptyToNull(request.Mobile?.VersionApplication),
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

                var jourTournee = ligne.Tournee?.JourTournee;
                var jourTourneeRetour = ligne.Retour?.JourTourneeRetour;

                var idTourneeLigne = await connection.QuerySingleAsync<long>(
                    """
                    INSERT INTO dbo.Mobile_TourneeLigne (
                        IdTourneeMobile,
                        IdLigneSource,
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
                        CodeTournee,
                        LibelleTournee,
                        JourTournee,
                        JourLibelle,
                        SchemaLivraison,
                        OrdreArret,
                        Horaire,
                        JourTourneeRetour,
                        JourRetourLibelle,
                        CodeTourneeRetour,
                        LibelleTourneeRetour,
                        Instructions,
                        CommentaireExceptionnel,
                        ZoneDechargement,
                        ZoneDechargementAffichee,
                        Zone,
                        PrecisionInfo,
                        Cle,
                        TypeLinge,
                        EstFerme,
                        DateFermeture,
                        MotifFermeture,
                        QuantiteLivree,
                        QuantiteReprise,
                        NbExpes,
                        NbRolls,
                        NbVetements,
                        NbTapis,
                        NbSacs,
                        NbRecuperes,
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
                        @NumClient,
                        @NomClient,
                        @NomAffiche,
                        @CodePDL,
                        @DescriptionPDL,
                        @AdresseLigne1,
                        @AdresseLigne2,
                        @AdresseLigne3,
                        @Ville,
                        @CodePostal,
                        @CodeTournee,
                        @LibelleTournee,
                        @JourTournee,
                        @JourLibelle,
                        @SchemaLivraison,
                        @OrdreArret,
                        @Horaire,
                        @JourTourneeRetour,
                        @JourRetourLibelle,
                        @CodeTourneeRetour,
                        @LibelleTourneeRetour,
                        @Instructions,
                        @CommentaireExceptionnel,
                        @ZoneDechargement,
                        @ZoneDechargementAffichee,
                        @Zone,
                        @PrecisionInfo,
                        @Cle,
                        NULL,
                        @EstFerme,
                        @DateFermeture,
                        @MotifFermeture,
                        @QuantiteLivree,
                        @QuantiteReprise,
                        @NbExpes,
                        @NbRolls,
                        @NbVetements,
                        @NbTapis,
                        @NbSacs,
                        @NbRecuperes,
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
                        NumClient = numClient,
                        NomClient = nomClient,
                        NomAffiche = EmptyToNull(ligne.Client?.NomAffiche),
                        CodePDL = EmptyToNull(ligne.PointLivraison?.CodePDL),
                        DescriptionPDL = EmptyToNull(ligne.PointLivraison?.DescriptionPDL),
                        AdresseLigne1 = EmptyToNull(ligne.PointLivraison?.AdresseLigne1),
                        AdresseLigne2 = EmptyToNull(ligne.PointLivraison?.AdresseLigne2),
                        AdresseLigne3 = EmptyToNull(ligne.PointLivraison?.AdresseLigne3),
                        Ville = EmptyToNull(ligne.PointLivraison?.Ville),
                        CodePostal = EmptyToNull(ligne.PointLivraison?.CodePostal),
                        CodeTournee = codeTournee,
                        LibelleTournee = EmptyToNull(ligne.Tournee?.LibelleTournee) ?? EmptyToNull(request.LibelleTournee),
                        JourTournee = jourTournee,
                        JourLibelle = EmptyToNull(ligne.Tournee?.JourLibelle) ?? GetJourLibelle(jourTournee),
                        SchemaLivraison = EmptyToNull(ligne.Tournee?.SchemaLivraison),
                        OrdreArret = ligne.OrdreArret,
                        Horaire = ligne.Horaire?.ToString(),
                        JourTourneeRetour = jourTourneeRetour,
                        JourRetourLibelle = EmptyToNull(ligne.Retour?.JourRetourLibelle) ?? GetJourLibelle(jourTourneeRetour),
                        CodeTourneeRetour = EmptyToNull(ligne.Retour?.CodeTourneeRetour),
                        LibelleTourneeRetour = EmptyToNull(ligne.Retour?.LibelleTourneeRetour),
                        Instructions = EmptyToNull(ligne.InfosLivreur?.Instructions),
                        CommentaireExceptionnel = EmptyToNull(ligne.InfosLivreur?.CommentaireExceptionnel),
                        ZoneDechargement = EmptyToNull(ligne.InfosLivreur?.ZoneDechargement),
                        ZoneDechargementAffichee = EmptyToNull(ligne.InfosLivreur?.ZoneDechargementAffichee),
                        Zone = EmptyToNull(ligne.InfosLivreur?.Zone),
                        PrecisionInfo = EmptyToNull(ligne.InfosLivreur?.Precision),
                        Cle = EmptyToNull(ligne.InfosLivreur?.Cle),
                        EstFerme = ligne.InfosLivreur?.EstFerme ?? false,
                        DateFermeture = ligne.InfosLivreur?.DateFermeture?.ToDateTime(TimeOnly.MinValue).Date,
                        MotifFermeture = EmptyToNull(ligne.InfosLivreur?.MotifFermeture),
                        QuantiteLivree = quantiteLivree,
                        QuantiteReprise = quantiteReprise,
                        NbExpes = nbExpes,
                        NbRolls = nbRolls,
                        NbVetements = nbVetements,
                        NbTapis = nbTapis,
                        NbSacs = nbSacs,
                        NbRecuperes = nbRecuperes,
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
                        INSERT INTO dbo.Mobile_TourneeLigneQuantite (
                            IdTourneeLigne,
                            CodeArticle,
                            LibelleArticle,
                            QuantiteLivreePrevue,
                            QuantiteLivree,
                            QuantiteRecuperee,
                            DateCreation,
                            DateModification
                        )
                        VALUES (
                            @IdTourneeLigne,
                            @CodeArticle,
                            @LibelleArticle,
                            @QuantiteLivreePrevue,
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
                            QuantiteLivreePrevue = quantite.QuantiteLivreePrevue,
                            QuantiteLivree = quantite.QuantiteLivree,
                            QuantiteRecuperee = quantite.QuantiteRecuperee,
                            Now = now
                        },
                        transaction);
                }
            }

            await connection.ExecuteAsync(
                """
                INSERT INTO dbo.Mobile_LogSynchronisation (
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
                    NomAppareil = EmptyToNull(request.Mobile?.NomAppareil),
                    VersionApplication = EmptyToNull(request.Mobile?.VersionApplication)
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

    private static DateTimeOffset? ParseOptionalDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return new DateTimeOffset(dateTime);
        }

        return null;
    }

    private static DateTimeOffset? ParseHeureValidation(string? value, DateTime dateTournee)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParse(value, out var heureSimple))
        {
            return new DateTimeOffset(dateTournee.Date.Add(heureSimple), DateTimeOffset.Now.Offset);
        }

        if (DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return new DateTimeOffset(dateTime);
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
                QuantiteLivreePrevue = q.QuantiteLivreePrevue,
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

    private static string? GetJourLibelle(int? jour)
    {
        return jour switch
        {
            1 => "Lundi",
            2 => "Mardi",
            3 => "Mercredi",
            4 => "Jeudi",
            5 => "Vendredi",
            6 => "Samedi",
            7 => "Dimanche",
            _ => null
        };
    }
}
