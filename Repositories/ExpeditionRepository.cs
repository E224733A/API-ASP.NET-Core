using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;
using Dapper;

namespace API_ASP.NET_Core.Repositories;

public sealed class ExpeditionRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public ExpeditionRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ExpeditionLotVerrouillageDto?> GetLotVerrouillageAsync(
        Guid idLotVerrouillage,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT TOP (1)
                lot.IdLotVerrouillage,
                lot.EmpreintePayload,
                lot.DateTournee,
                lot.CodeTournee,
                preparation.IdPreparationExpedition
            FROM dbo.Mobile_ExpeditionLotVerrouillage AS lot
            LEFT JOIN dbo.Mobile_ExpeditionPreparation AS preparation
                ON preparation.IdLotVerrouillage = lot.IdLotVerrouillage
            WHERE lot.IdLotVerrouillage = @IdLotVerrouillage
            ORDER BY preparation.IdPreparationExpedition;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExpeditionLotVerrouillageDto>(
            new CommandDefinition(
                sql,
                new
                {
                    IdLotVerrouillage = idLotVerrouillage
                },
                cancellationToken: cancellationToken));
    }

    public async Task<ExpeditionPreparationEtatDto?> GetPreparationEtatAsync(
        DateTime dateTournee,
        string codeTournee,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT TOP (1)
                IdPreparationExpedition,
                DateTournee,
                CodeTournee,
                StatutPreparation,
                EstVerrouille,
                IdLotVerrouillage
            FROM dbo.Mobile_ExpeditionPreparation
            WHERE DateTournee = @DateTournee
              AND CodeTournee = @CodeTournee;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExpeditionPreparationEtatDto>(
            new CommandDefinition(
                sql,
                new
                {
                    DateTournee = dateTournee.Date,
                    CodeTournee = codeTournee.Trim()
                },
                cancellationToken: cancellationToken));
    }

    public async Task<ExpeditionVerrouillageLotSaveResult> EnregistrerVerrouillageLotAsync(
        ExpeditionVerrouillageLotRequest request,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string empreintePayload,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();

        var now = DateTimeOffset.Now;
        var nombreTournees = request.Tournees.Count;
        var nombreLignes = request.Tournees.Sum(tournee => tournee.Lignes?.Count ?? 0);
        var nombreQuantites = request.Tournees
            .SelectMany(tournee => tournee.Lignes ?? new List<ExpeditionVerrouillageLigneRequest>())
            .SelectMany(ligne => ligne.QuantitesPrevues ?? new List<ExpeditionQuantitePrevueRequest>())
            .Count(quantite =>
                !IsRollsVides(quantite.CodeArticle)
                && quantite.QuantiteLivreePrevue.HasValue);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO dbo.Mobile_ExpeditionLotVerrouillage (
                        IdLotVerrouillage,
                        EmpreintePayload,
                        DateTournee,
                        CodeTournee,
                        LibelleTournee,
                        StatutLot,
                        NombrePreparations,
                        NombreLignes,
                        NombreQuantites,
                        AdresseIP,
                        MessageRetour,
                        DateReceptionApi,
                        DateSauvegardeSql,
                        DateCreation
                    )
                    VALUES (
                        @IdLotVerrouillage,
                        @EmpreintePayload,
                        @DateTournee,
                        @CodeTournee,
                        @LibelleTournee,
                        N'VERROUILLE',
                        @NombrePreparations,
                        @NombreLignes,
                        @NombreQuantites,
                        @AdresseIP,
                        @MessageRetour,
                        @Now,
                        @Now,
                        @Now
                    );
                    """,
                    new
                    {
                        IdLotVerrouillage = idLotVerrouillageTechnique,
                        EmpreintePayload = empreintePayload,
                        DateTournee = dateTournee.Date,
                        CodeTournee = "GLOBAL",
                        LibelleTournee = $"Lot global Expédition {request.IdLotVerrouillage}",
                        NombrePreparations = nombreTournees,
                        NombreLignes = nombreLignes,
                        NombreQuantites = nombreQuantites,
                        AdresseIP = NormalizeNullable(adresseIp),
                        MessageRetour = "Préparations Expédition verrouillées.",
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var tournee in request.Tournees)
            {
                var idPreparationExpedition = await UpsertPreparationExpeditionAsync(
                    connection,
                    transaction,
                    tournee,
                    dateTournee,
                    idLotVerrouillageTechnique,
                    empreintePayload,
                    adresseIp,
                    now,
                    cancellationToken);

                await DesactiverAnciennesQuantitesAsync(
                    connection,
                    transaction,
                    idPreparationExpedition,
                    now,
                    cancellationToken);

                foreach (var ligne in tournee.Lignes)
                {
                    await EnregistrerQuantitesLigneAsync(
                        connection,
                        transaction,
                        idPreparationExpedition,
                        ligne,
                        now,
                        cancellationToken);

                    await EnregistrerCommentaireLigneAsync(
                        connection,
                        transaction,
                        request,
                        tournee,
                        ligne,
                        dateTournee,
                        now,
                        cancellationToken);
                }

                await EnregistrerHistoriqueTourneeAsync(
                    connection,
                    transaction,
                    request,
                    tournee,
                    idPreparationExpedition,
                    dateTournee,
                    idLotVerrouillageTechnique,
                    adresseIp,
                    now,
                    cancellationToken);
            }

            await EnregistrerLogVerrouillageAsync(
                connection,
                transaction,
                request,
                dateTournee,
                idLotVerrouillageTechnique,
                empreintePayload,
                adresseIp,
                now,
                cancellationToken);

            transaction.Commit();

            return new ExpeditionVerrouillageLotSaveResult
            {
                NombreTourneesVerrouillees = nombreTournees,
                NombreLignesVerrouillees = nombreLignes,
                DateReceptionApi = now,
                DateSauvegardeSql = DateTimeOffset.Now
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<long> UpsertPreparationExpeditionAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageTourneeRequest tournee,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string empreintePayload,
        string? adresseIp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleAsync<long>(
            new CommandDefinition(
                """
                DECLARE @IdPreparationExpedition BIGINT;

                SELECT @IdPreparationExpedition = IdPreparationExpedition
                FROM dbo.Mobile_ExpeditionPreparation WITH (UPDLOCK, HOLDLOCK)
                WHERE DateTournee = @DateTournee
                  AND CodeTournee = @CodeTournee;

                IF @IdPreparationExpedition IS NULL
                BEGIN
                    INSERT INTO dbo.Mobile_ExpeditionPreparation (
                        DateTournee,
                        CodeTournee,
                        LibelleTournee,
                        StatutPreparation,
                        EstVerrouille,
                        DateVerrouillage,
                        IdLotVerrouillage,
                        EmpreintePayload,
                        AdresseIPVerrouillage,
                        DateCreation,
                        DateModification
                    )
                    VALUES (
                        @DateTournee,
                        @CodeTournee,
                        @LibelleTournee,
                        N'VERROUILLEE',
                        1,
                        @Now,
                        @IdLotVerrouillage,
                        @EmpreintePayload,
                        @AdresseIP,
                        @Now,
                        NULL
                    );

                    SET @IdPreparationExpedition = CONVERT(BIGINT, SCOPE_IDENTITY());
                END
                ELSE
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM dbo.Mobile_ExpeditionPreparation
                        WHERE IdPreparationExpedition = @IdPreparationExpedition
                          AND IdLotVerrouillage <> @IdLotVerrouillage
                    )
                    BEGIN
                        THROW 51001, 'La préparation Expédition est déjà verrouillée avec un autre lot.', 1;
                    END;

                    UPDATE dbo.Mobile_ExpeditionPreparation
                    SET
                        LibelleTournee = @LibelleTournee,
                        StatutPreparation = N'VERROUILLEE',
                        EstVerrouille = 1,
                        DateVerrouillage = COALESCE(DateVerrouillage, @Now),
                        IdLotVerrouillage = @IdLotVerrouillage,
                        EmpreintePayload = @EmpreintePayload,
                        AdresseIPVerrouillage = COALESCE(AdresseIPVerrouillage, @AdresseIP),
                        DateModification = @Now
                    WHERE IdPreparationExpedition = @IdPreparationExpedition;
                END;

                SELECT @IdPreparationExpedition;
                """,
                new
                {
                    DateTournee = dateTournee.Date,
                    CodeTournee = tournee.CodeTournee.Trim(),
                    LibelleTournee = NormalizeNullable(tournee.LibelleTournee),
                    IdLotVerrouillage = idLotVerrouillageTechnique,
                    EmpreintePayload = empreintePayload,
                    AdresseIP = NormalizeNullable(adresseIp),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task DesactiverAnciennesQuantitesAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        long idPreparationExpedition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE dbo.Mobile_ExpeditionPreparationLigne
                SET
                    Actif = 0,
                    DateModification = @Now
                WHERE IdPreparationExpedition = @IdPreparationExpedition
                  AND Actif = 1;
                """,
                new
                {
                    IdPreparationExpedition = idPreparationExpedition,
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task EnregistrerQuantitesLigneAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        long idPreparationExpedition,
        ExpeditionVerrouillageLigneRequest ligne,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (ligne.QuantitesPrevues is null || ligne.QuantitesPrevues.Count == 0)
        {
            return;
        }

        foreach (var quantite in ligne.QuantitesPrevues)
        {
            if (IsRollsVides(quantite.CodeArticle) || !quantite.QuantiteLivreePrevue.HasValue)
            {
                continue;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO dbo.Mobile_ExpeditionPreparationLigne (
                        IdPreparationExpedition,
                        IdLigneSource,
                        OrdreArret,
                        NumClient,
                        NomClient,
                        CodePDL,
                        DescriptionPDL,
                        CodeArticle,
                        LibelleArticle,
                        QuantiteLivreePrevue,
                        Actif,
                        DateCreation
                    )
                    VALUES (
                        @IdPreparationExpedition,
                        @IdLigneSource,
                        @OrdreArret,
                        @NumClient,
                        @NomClient,
                        @CodePDL,
                        @DescriptionPDL,
                        @CodeArticle,
                        @LibelleArticle,
                        @QuantiteLivreePrevue,
                        1,
                        @Now
                    );
                    """,
                    new
                    {
                        IdPreparationExpedition = idPreparationExpedition,
                        IdLigneSource = ligne.IdLigneSource.Trim(),
                        OrdreArret = ligne.OrdreArret,
                        NumClient = ligne.Client.NumClient.Trim(),
                        NomClient = NormalizeNullable(ligne.Client.NomClient),
                        CodePDL = NormalizeNullable(ligne.PointLivraison?.CodePDL),
                        DescriptionPDL = NormalizeNullable(ligne.PointLivraison?.DescriptionPDL),
                        CodeArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                        LibelleArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                        QuantiteLivreePrevue = quantite.QuantiteLivreePrevue.Value,
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    private static async Task EnregistrerCommentaireLigneAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotRequest request,
        ExpeditionVerrouillageTourneeRequest tournee,
        ExpeditionVerrouillageLigneRequest ligne,
        DateTime dateTournee,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (ligne.CommentaireExceptionnel is null)
        {
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE dbo.Mobile_CommentaireExceptionnel
                SET
                    Actif = 0,
                    DateModification = @Now
                WHERE DateTournee = @DateTournee
                  AND CodeTournee = @CodeTournee
                  AND IdLigneSource = @IdLigneSource
                  AND Actif = 1;
                """,
                new
                {
                    DateTournee = dateTournee.Date,
                    CodeTournee = tournee.CodeTournee.Trim(),
                    IdLigneSource = ligne.IdLigneSource.Trim(),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(ligne.CommentaireExceptionnel))
        {
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO dbo.Mobile_CommentaireExceptionnel (
                    DateTournee,
                    CodeTournee,
                    IdLigneSource,
                    NumClient,
                    CodePDL,
                    Commentaire,
                    Actif,
                    CreePar,
                    DateCreation
                )
                VALUES (
                    @DateTournee,
                    @CodeTournee,
                    @IdLigneSource,
                    @NumClient,
                    @CodePDL,
                    @Commentaire,
                    1,
                    @CreePar,
                    @Now
                );
                """,
                new
                {
                    DateTournee = dateTournee.Date,
                    CodeTournee = tournee.CodeTournee.Trim(),
                    IdLigneSource = ligne.IdLigneSource.Trim(),
                    NumClient = ligne.Client.NumClient.Trim(),
                    CodePDL = NormalizeNullable(ligne.PointLivraison?.CodePDL),
                    Commentaire = ligne.CommentaireExceptionnel.Trim(),
                    CreePar = NormalizeNullable(ligne.DerniereModification?.Utilisateur) ?? NormalizeNullable(request.Source),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task EnregistrerHistoriqueTourneeAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotRequest request,
        ExpeditionVerrouillageTourneeRequest tournee,
        long idPreparationExpedition,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string? adresseIp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO dbo.Mobile_ExpeditionPreparationHistorique (
                    IdPreparationExpedition,
                    DateTournee,
                    CodeTournee,
                    ActionHistorique,
                    Commentaire,
                    AdresseIP,
                    DateEvenement
                )
                VALUES (
                    @IdPreparationExpedition,
                    @DateTournee,
                    @CodeTournee,
                    N'VERROUILLAGE',
                    @Commentaire,
                    @AdresseIP,
                    @Now
                );
                """,
                new
                {
                    IdPreparationExpedition = idPreparationExpedition,
                    DateTournee = dateTournee.Date,
                    CodeTournee = tournee.CodeTournee.Trim(),
                    Commentaire = $"Lot global Expédition verrouillé : {request.IdLotVerrouillage} ({idLotVerrouillageTechnique:D})",
                    AdresseIP = NormalizeNullable(adresseIp),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task EnregistrerLogVerrouillageAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotRequest request,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string empreintePayload,
        string? adresseIp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO dbo.Mobile_LogSynchronisation (
                    TypeEvenement,
                    Niveau,
                    Message,
                    DetailTechnique,
                    AdresseIP,
                    DateEvenement
                )
                VALUES (
                    N'VERROUILLAGE_EXPEDITION_PREPARATION',
                    N'INFO',
                    N'Préparations Expédition verrouillées.',
                    @DetailTechnique,
                    @AdresseIP,
                    @Now
                );
                """,
                new
                {
                    DetailTechnique = $"DateTournee={dateTournee:yyyy-MM-dd}; IdLotVerrouillageMetier={request.IdLotVerrouillage}; IdLotVerrouillageTechnique={idLotVerrouillageTechnique:D}; EmpreintePayload={empreintePayload}",
                    AdresseIP = NormalizeNullable(adresseIp),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static bool IsRollsVides(string? codeArticle)
    {
        return string.Equals(codeArticle?.Trim(), ArticlesSaisissables.RollsVides, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
