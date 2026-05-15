using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;
using Dapper;

namespace API_ASP.NET_Core.Repositories;

public sealed record ExpeditionLotVerrouillageRecord
{
    public Guid IdLotVerrouillage { get; init; }
    public string Statut { get; init; } = string.Empty;
    public DateTimeOffset DateReceptionApi { get; init; }
    public DateTimeOffset? DateTraitement { get; init; }
    public int NombreTournees { get; init; }
    public int NombreLignes { get; init; }
    public int NombreQuantites { get; init; }
}

public sealed record ExpeditionSaveResult
{
    public int NombreTournees { get; init; }
    public int NombreLignes { get; init; }
    public int NombreQuantites { get; init; }
    public DateTimeOffset DateSauvegardeSql { get; init; }
}

public sealed class ExpeditionRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public ExpeditionRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ExpeditionLotVerrouillageRecord?> GetLotVerrouillageAsync(
        Guid idLotVerrouillage,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT TOP 1
                IdLotVerrouillage,
                Statut,
                DateReceptionApi,
                DateTraitement,
                NombreTournees,
                NombreLignes,
                NombreQuantites
            FROM dbo.Mobile_ExpeditionLotVerrouillage
            WHERE IdLotVerrouillage = @IdLotVerrouillage;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExpeditionLotVerrouillageRecord>(
            new CommandDefinition(
                sql,
                new { IdLotVerrouillage = idLotVerrouillage },
                cancellationToken: cancellationToken));
    }

    public async Task<HashSet<string>> GetTourneesVerrouilleesAsync(
        DateOnly dateTournee,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT LTRIM(RTRIM(CodeTournee))
            FROM dbo.Mobile_PreRemplissageTournee
            WHERE DateTournee = @DateTournee
              AND EstVerrouille = 1;
            """;

        var rows = await connection.QueryAsync<string>(
            new CommandDefinition(
                sql,
                new { DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date },
                cancellationToken: cancellationToken));

        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ExpeditionSaveResult> SaveVerrouillageAsync(
        ExpeditionVerrouillageRequest request,
        DateOnly dateTournee,
        DateTimeOffset dateReceptionApi,
        string? adresseIp,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var nombreTournees = request.Tournees.Count;
        var nombreLignes = request.Tournees.Sum(tournee => tournee.Lignes.Count);
        var nombreQuantites = request.Tournees.Sum(tournee => tournee.Lignes.Sum(ligne => ligne.Quantites.Count));
        var dateSql = dateReceptionApi;

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.Mobile_ExpeditionLotVerrouillage (
                    IdLotVerrouillage,
                    SchemaVersion,
                    DateTournee,
                    DateReceptionApi,
                    DateDeclenchementWeb,
                    FuseauHoraireWeb,
                    Statut,
                    NombreTournees,
                    NombreLignes,
                    NombreQuantites,
                    AdresseIP,
                    Message
                )
                VALUES (
                    @IdLotVerrouillage,
                    @SchemaVersion,
                    @DateTournee,
                    @DateReceptionApi,
                    @DateDeclenchementWeb,
                    @FuseauHoraireWeb,
                    N'EN_COURS',
                    @NombreTournees,
                    @NombreLignes,
                    @NombreQuantites,
                    @AdresseIP,
                    N'Verrouillage Expédition reçu par l''API.'
                );
                """,
                new
                {
                    request.IdLotVerrouillage,
                    request.SchemaVersion,
                    DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                    DateReceptionApi = dateReceptionApi,
                    request.DateDeclenchementWeb,
                    request.FuseauHoraireWeb,
                    NombreTournees = nombreTournees,
                    NombreLignes = nombreLignes,
                    NombreQuantites = nombreQuantites,
                    AdresseIP = adresseIp
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var tournee in request.Tournees)
            {
                var idPreRemplissageTournee = await UpsertTourneeAsync(
                    connection,
                    transaction,
                    request.IdLotVerrouillage,
                    dateTournee,
                    tournee,
                    dateSql,
                    cancellationToken);

                await DesactiverAnciennesQuantitesAsync(
                    connection,
                    transaction,
                    idPreRemplissageTournee,
                    dateSql,
                    cancellationToken);

                foreach (var ligne in tournee.Lignes)
                {
                    await UpsertCommentaireExceptionnelAsync(
                        connection,
                        transaction,
                        dateTournee,
                        ligne,
                        dateSql,
                        cancellationToken);

                    foreach (var quantite in ligne.Quantites)
                    {
                        await InsertQuantiteAsync(
                            connection,
                            transaction,
                            idPreRemplissageTournee,
                            ligne,
                            quantite,
                            dateSql,
                            cancellationToken);
                    }
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO dbo.Mobile_PreRemplissageHistorique (
                        IdPreRemplissageTournee,
                        DateTournee,
                        CodeTournee,
                        ActionHistorique,
                        Commentaire,
                        AdresseIP,
                        DateEvenement
                    )
                    VALUES (
                        @IdPreRemplissageTournee,
                        @DateTournee,
                        @CodeTournee,
                        N'VERROUILLAGE',
                        @Commentaire,
                        @AdresseIP,
                        @DateEvenement
                    );
                    """,
                    new
                    {
                        IdPreRemplissageTournee = idPreRemplissageTournee,
                        DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                        CodeTournee = tournee.CodeTournee.Trim(),
                        Commentaire = $"Verrouillage Expédition par lot {request.IdLotVerrouillage}.",
                        AdresseIP = adresseIp,
                        DateEvenement = dateSql
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Mobile_ExpeditionLotVerrouillage
                SET Statut = N'SUCCESS',
                    DateTraitement = @DateTraitement,
                    Message = N'Préparations Expédition sauvegardées et verrouillées avec succès.'
                WHERE IdLotVerrouillage = @IdLotVerrouillage;
                """,
                new
                {
                    request.IdLotVerrouillage,
                    DateTraitement = dateSql
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.Mobile_LogSynchronisation (
                    DateEvenement,
                    TypeEvenement,
                    Niveau,
                    Message,
                    DetailTechnique,
                    AdresseIP
                )
                VALUES (
                    @DateEvenement,
                    N'BLOCAGE_PRE_REMPLISSAGE',
                    N'INFO',
                    N'Verrouillage Expédition terminé.',
                    @DetailTechnique,
                    @AdresseIP
                );
                """,
                new
                {
                    DateEvenement = dateSql,
                    DetailTechnique = $"Lot {request.IdLotVerrouillage} - {nombreTournees} tournée(s), {nombreLignes} ligne(s), {nombreQuantites} quantité(s).",
                    AdresseIP = adresseIp
                },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return new ExpeditionSaveResult
            {
                NombreTournees = nombreTournees,
                NombreLignes = nombreLignes,
                NombreQuantites = nombreQuantites,
                DateSauvegardeSql = dateSql
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<long> UpsertTourneeAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        Guid idLotVerrouillage,
        DateOnly dateTournee,
        ExpeditionTourneeVerrouillageDto tournee,
        DateTimeOffset dateSql,
        CancellationToken cancellationToken)
    {
        var idExisting = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            """
            SELECT IdPreRemplissageTournee
            FROM dbo.Mobile_PreRemplissageTournee
            WHERE DateTournee = @DateTournee
              AND CodeTournee = @CodeTournee;
            """,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                CodeTournee = tournee.CodeTournee.Trim()
            },
            transaction,
            cancellationToken: cancellationToken));

        if (idExisting.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Mobile_PreRemplissageTournee
                SET LibelleTournee = @LibelleTournee,
                    EstVerrouille = 1,
                    DateVerrouillage = @DateVerrouillage,
                    IdLotVerrouillage = @IdLotVerrouillage,
                    DateModification = @DateModification
                WHERE IdPreRemplissageTournee = @IdPreRemplissageTournee
                  AND EstVerrouille = 0;
                """,
                new
                {
                    IdPreRemplissageTournee = idExisting.Value,
                    LibelleTournee = tournee.LibelleTournee,
                    DateVerrouillage = dateSql,
                    IdLotVerrouillage = idLotVerrouillage,
                    DateModification = dateSql
                },
                transaction,
                cancellationToken: cancellationToken));

            return idExisting.Value;
        }

        return await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO dbo.Mobile_PreRemplissageTournee (
                DateTournee,
                CodeTournee,
                LibelleTournee,
                EstVerrouille,
                DateVerrouillage,
                IdLotVerrouillage,
                DateCreation,
                DateModification
            )
            OUTPUT inserted.IdPreRemplissageTournee
            VALUES (
                @DateTournee,
                @CodeTournee,
                @LibelleTournee,
                1,
                @DateVerrouillage,
                @IdLotVerrouillage,
                @DateCreation,
                @DateModification
            );
            """,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                CodeTournee = tournee.CodeTournee.Trim(),
                LibelleTournee = tournee.LibelleTournee,
                DateVerrouillage = dateSql,
                IdLotVerrouillage = idLotVerrouillage,
                DateCreation = dateSql,
                DateModification = dateSql
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task DesactiverAnciennesQuantitesAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        long idPreRemplissageTournee,
        DateTimeOffset dateSql,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.Mobile_PreRemplissageQuantite
            SET Actif = 0,
                DateModification = @DateModification
            WHERE IdPreRemplissageTournee = @IdPreRemplissageTournee
              AND Actif = 1;
            """,
            new
            {
                IdPreRemplissageTournee = idPreRemplissageTournee,
                DateModification = dateSql
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task InsertQuantiteAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        long idPreRemplissageTournee,
        ExpeditionLigneVerrouillageDto ligne,
        ExpeditionQuantiteVerrouillageDto quantite,
        DateTimeOffset dateSql,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.Mobile_PreRemplissageQuantite (
                IdPreRemplissageTournee,
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
                DateCreation,
                DateModification
            )
            VALUES (
                @IdPreRemplissageTournee,
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
                @DateCreation,
                @DateModification
            );
            """,
            new
            {
                IdPreRemplissageTournee = idPreRemplissageTournee,
                IdLigneSource = ligne.IdLigneSource.Trim(),
                ligne.OrdreArret,
                NumClient = ligne.NumClient.Trim(),
                ligne.NomClient,
                CodePDL = NormalizeNullable(ligne.CodePDL),
                ligne.DescriptionPDL,
                CodeArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                quantite.LibelleArticle,
                quantite.QuantiteLivreePrevue,
                DateCreation = dateSql,
                DateModification = dateSql
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task UpsertCommentaireExceptionnelAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        DateOnly dateTournee,
        ExpeditionLigneVerrouillageDto ligne,
        DateTimeOffset dateSql,
        CancellationToken cancellationToken)
    {
        var commentaire = NormalizeNullable(ligne.CommentaireExceptionnel);
        var codePdl = NormalizeNullable(ligne.CodePDL);

        if (commentaire is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Mobile_CommentaireExceptionnel
                SET Actif = 0,
                    DateModification = @DateModification,
                    ModifiePar = N'EXPEDITION_WEB'
                WHERE DateTournee = @DateTournee
                  AND NumClient = @NumClient
                  AND ((CodePDL = @CodePDL) OR (CodePDL IS NULL AND @CodePDL IS NULL))
                  AND Actif = 1;
                """,
                new
                {
                    DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                    NumClient = ligne.NumClient.Trim(),
                    CodePDL = codePdl,
                    DateModification = dateSql
                },
                transaction,
                cancellationToken: cancellationToken));

            return;
        }

        var idExisting = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            """
            SELECT IdCommentaireExceptionnel
            FROM dbo.Mobile_CommentaireExceptionnel
            WHERE DateTournee = @DateTournee
              AND NumClient = @NumClient
              AND ((CodePDL = @CodePDL) OR (CodePDL IS NULL AND @CodePDL IS NULL))
              AND Actif = 1;
            """,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                NumClient = ligne.NumClient.Trim(),
                CodePDL = codePdl
            },
            transaction,
            cancellationToken: cancellationToken));

        if (idExisting.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE dbo.Mobile_CommentaireExceptionnel
                SET Commentaire = @Commentaire,
                    DateModification = @DateModification,
                    ModifiePar = N'EXPEDITION_WEB'
                WHERE IdCommentaireExceptionnel = @IdCommentaireExceptionnel;
                """,
                new
                {
                    IdCommentaireExceptionnel = idExisting.Value,
                    Commentaire = commentaire,
                    DateModification = dateSql
                },
                transaction,
                cancellationToken: cancellationToken));

            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.Mobile_CommentaireExceptionnel (
                DateTournee,
                NumClient,
                CodePDL,
                Commentaire,
                Actif,
                CreePar,
                DateCreation,
                DateModification
            )
            VALUES (
                @DateTournee,
                @NumClient,
                @CodePDL,
                @Commentaire,
                1,
                N'EXPEDITION_WEB',
                @DateCreation,
                @DateModification
            );
            """,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                NumClient = ligne.NumClient.Trim(),
                CodePDL = codePdl,
                Commentaire = commentaire,
                DateCreation = dateSql,
                DateModification = dateSql
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
