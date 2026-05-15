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
                IdLotVerrouillage,
                EmpreintePayload,
                DateTournee,
                CodeTournee,
                IdPreRemplissageTournee
            FROM dbo.Mobile_ExpeditionLotVerrouillage
            WHERE IdLotVerrouillage = @IdLotVerrouillage;
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
                IdPreRemplissageTournee,
                DateTournee,
                CodeTournee,
                EstVerrouille,
                IdLotVerrouillage
            FROM dbo.Mobile_PreRemplissageTournee
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

    public async Task<long> EnregistrerVerrouillageAsync(
        ExpeditionVerrouillageRequest request,
        DateTime dateTournee,
        Guid idLotVerrouillage,
        string empreintePayload,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.Now;

        try
        {
            var nombreQuantites = request.Lignes
                .SelectMany(ligne => ligne.Quantites ?? new List<ExpeditionVerrouillageQuantiteRequest>())
                .Count(quantite =>
                    !IsRollsVides(quantite.CodeArticle)
                    && quantite.QuantiteLivreePrevue.HasValue);

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
                        NombreLignes,
                        NombreQuantites,
                        AdresseIP,
                        DateCreation
                    )
                    VALUES (
                        @IdLotVerrouillage,
                        @EmpreintePayload,
                        @DateTournee,
                        @CodeTournee,
                        @LibelleTournee,
                        N'VERROUILLE',
                        @NombreLignes,
                        @NombreQuantites,
                        @AdresseIP,
                        @Now
                    );
                    """,
                    new
                    {
                        IdLotVerrouillage = idLotVerrouillage,
                        EmpreintePayload = empreintePayload,
                        DateTournee = dateTournee.Date,
                        CodeTournee = request.CodeTournee.Trim(),
                        LibelleTournee = NormalizeNullable(request.LibelleTournee),
                        NombreLignes = request.Lignes.Count,
                        NombreQuantites = nombreQuantites,
                        AdresseIP = NormalizeNullable(adresseIp),
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            var idPreRemplissageTournee = await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    """
                    DECLARE @IdPreRemplissageTournee BIGINT;

                    SELECT @IdPreRemplissageTournee = IdPreRemplissageTournee
                    FROM dbo.Mobile_PreRemplissageTournee WITH (UPDLOCK, HOLDLOCK)
                    WHERE DateTournee = @DateTournee
                      AND CodeTournee = @CodeTournee;

                    IF @IdPreRemplissageTournee IS NULL
                    BEGIN
                        INSERT INTO dbo.Mobile_PreRemplissageTournee (
                            DateTournee,
                            CodeTournee,
                            LibelleTournee,
                            EstVerrouille,
                            DateVerrouillage,
                            IdLotVerrouillage,
                            EmpreintePayload,
                            DateCreation,
                            DateModification
                        )
                        VALUES (
                            @DateTournee,
                            @CodeTournee,
                            @LibelleTournee,
                            1,
                            @Now,
                            @IdLotVerrouillage,
                            @EmpreintePayload,
                            @Now,
                            NULL
                        );

                        SET @IdPreRemplissageTournee = CONVERT(BIGINT, SCOPE_IDENTITY());
                    END
                    ELSE
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM dbo.Mobile_PreRemplissageTournee
                            WHERE IdPreRemplissageTournee = @IdPreRemplissageTournee
                              AND EstVerrouille = 1
                              AND IdLotVerrouillage IS NOT NULL
                              AND IdLotVerrouillage <> @IdLotVerrouillage
                        )
                        BEGIN
                            THROW 51001, 'La préparation est déjà verrouillée avec un autre lot.', 1;
                        END;

                        UPDATE dbo.Mobile_PreRemplissageTournee
                        SET
                            LibelleTournee = @LibelleTournee,
                            EstVerrouille = 1,
                            DateVerrouillage = COALESCE(DateVerrouillage, @Now),
                            IdLotVerrouillage = @IdLotVerrouillage,
                            EmpreintePayload = @EmpreintePayload,
                            DateModification = @Now
                        WHERE IdPreRemplissageTournee = @IdPreRemplissageTournee;
                    END;

                    SELECT @IdPreRemplissageTournee;
                    """,
                    new
                    {
                        DateTournee = dateTournee.Date,
                        CodeTournee = request.CodeTournee.Trim(),
                        LibelleTournee = NormalizeNullable(request.LibelleTournee),
                        IdLotVerrouillage = idLotVerrouillage,
                        EmpreintePayload = empreintePayload,
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE dbo.Mobile_PreRemplissageQuantite
                    SET
                        Actif = 0,
                        DateModification = @Now
                    WHERE IdPreRemplissageTournee = @IdPreRemplissageTournee
                      AND Actif = 1;
                    """,
                    new
                    {
                        IdPreRemplissageTournee = idPreRemplissageTournee,
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var ligne in request.Lignes)
            {
                await EnregistrerQuantitesLigneAsync(
                    connection,
                    transaction,
                    idPreRemplissageTournee,
                    ligne,
                    now,
                    cancellationToken);

                await EnregistrerCommentaireLigneAsync(
                    connection,
                    transaction,
                    request,
                    ligne,
                    dateTournee,
                    now,
                    cancellationToken);
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE dbo.Mobile_ExpeditionLotVerrouillage
                    SET
                        IdPreRemplissageTournee = @IdPreRemplissageTournee,
                        DateModification = @Now
                    WHERE IdLotVerrouillage = @IdLotVerrouillage;
                    """,
                    new
                    {
                        IdPreRemplissageTournee = idPreRemplissageTournee,
                        IdLotVerrouillage = idLotVerrouillage,
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
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
                        @Now
                    );
                    """,
                    new
                    {
                        IdPreRemplissageTournee = idPreRemplissageTournee,
                        DateTournee = dateTournee.Date,
                        CodeTournee = request.CodeTournee.Trim(),
                        Commentaire = $"Lot Expédition verrouillé : {idLotVerrouillage:D}",
                        AdresseIP = NormalizeNullable(adresseIp),
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

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
                        N'BLOCAGE_PRE_REMPLISSAGE',
                        N'INFO',
                        N'Préparation Expédition verrouillée.',
                        @DetailTechnique,
                        @AdresseIP,
                        @Now
                    );
                    """,
                    new
                    {
                        DetailTechnique = $"DateTournee={dateTournee:yyyy-MM-dd}; CodeTournee={request.CodeTournee.Trim()}; IdLotVerrouillage={idLotVerrouillage:D}; EmpreintePayload={empreintePayload}",
                        AdresseIP = NormalizeNullable(adresseIp),
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();
            return idPreRemplissageTournee;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task EnregistrerQuantitesLigneAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        long idPreRemplissageTournee,
        ExpeditionVerrouillageLigneRequest ligne,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (ligne.Quantites is null || ligne.Quantites.Count == 0)
        {
            return;
        }

        foreach (var quantite in ligne.Quantites)
        {
            if (IsRollsVides(quantite.CodeArticle) || !quantite.QuantiteLivreePrevue.HasValue)
            {
                continue;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
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
                        DateCreation
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
                        @Now
                    );
                    """,
                    new
                    {
                        IdPreRemplissageTournee = idPreRemplissageTournee,
                        IdLigneSource = ligne.IdLigneSource.Trim(),
                        OrdreArret = ligne.OrdreArret,
                        NumClient = ligne.NumClient.Trim(),
                        NomClient = NormalizeNullable(ligne.NomClient),
                        CodePDL = NormalizeNullable(ligne.CodePDL),
                        DescriptionPDL = NormalizeNullable(ligne.DescriptionPDL),
                        CodeArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                        LibelleArticle = NormalizeNullable(quantite.Libelle) ?? quantite.CodeArticle.Trim().ToUpperInvariant(),
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
        ExpeditionVerrouillageRequest request,
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
                    CodeTournee = request.CodeTournee.Trim(),
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
                    CodeTournee = request.CodeTournee.Trim(),
                    IdLigneSource = ligne.IdLigneSource.Trim(),
                    NumClient = ligne.NumClient.Trim(),
                    CodePDL = NormalizeNullable(ligne.CodePDL),
                    Commentaire = ligne.CommentaireExceptionnel.Trim(),
                    CreePar = NormalizeNullable(request.Utilisateur?.Identifiant) ?? NormalizeNullable(request.Utilisateur?.NomAffiche),
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
