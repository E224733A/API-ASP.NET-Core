using Dapper;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Data;
using System.Data;

namespace API_ASP.NET_Core.Repositories;

public record LivreurRecord
{
    public string CodeLivreur { get; init; } = string.Empty;
    public string NomLivreur { get; init; } = string.Empty;
}

public record TourneeDisponibleRecord
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
    public int NombrePoints { get; init; }
}

public record ArticleSaisissableRecord
{
    public string CodeArticle { get; init; } = string.Empty;
    public string LibelleArticle { get; init; } = string.Empty;
    public int OrdreAffichage { get; init; }
}

public record CommentaireExceptionnelRecord
{
    public string? IdLigneSource { get; init; }
    public string? CodeTournee { get; init; }
    public string NumClient { get; init; } = string.Empty;
    public string? CodePDL { get; init; }
    public string Commentaire { get; init; } = string.Empty;
}

public record PreRemplissageQuantiteRecord
{
    public string IdLigneSource { get; init; } = string.Empty;
    public string NumClient { get; init; } = string.Empty;
    public string? CodePDL { get; init; }
    public string CodeArticle { get; init; } = string.Empty;
    public string? LibelleArticle { get; init; }
    public int? QuantiteLivreePrevue { get; init; }
}

public record TourneeLigneRecord
{
    public string NumClient { get; init; } = string.Empty;
    public string NomClient { get; init; } = string.Empty;
    public string? NomAffiche { get; init; }

    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }

    public string? AdresseLigne1 { get; init; }
    public string? AdresseLigne2 { get; init; }
    public string? AdresseLigne3 { get; init; }
    public string? Ville { get; init; }
    public string? CodePostal { get; init; }

    public int? OrdreArret { get; init; }
    public int? Horaire { get; init; }

    public int? JourTournee { get; init; }

    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }

    public int? JourTourneeRetour { get; init; }
    public string? CodeTourneeRetour { get; init; }
    public string? LibelleTourneeRetour { get; init; }

    public string? SchemaLivraison { get; init; }

    public string? Instructions { get; init; }
    public string? ZoneDechargement { get; init; }
    public string? Zone { get; init; }
    public string? Precision { get; init; }
    public string? Cle { get; init; }

    public bool EstFerme { get; init; }
    public DateTime? DateFermeture { get; init; }
    public string? MotifFermeture { get; init; }
}

public class TourneesRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public TourneesRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LivreurRecord?> GetLivreurAsync(string codeLivreur)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        const string sql = """
            SELECT TOP 1
                CAST(DRIVERNUMERO AS NVARCHAR(50)) AS CodeLivreur,
                DRIVERNAME AS NomLivreur
            FROM v_chauffeurs
            WHERE LTRIM(RTRIM(CAST(DRIVERNUMERO AS NVARCHAR(50)))) = @CodeLivreur;
            """;

        return await connection.QuerySingleOrDefaultAsync<LivreurRecord>(
            sql,
            new
            {
                CodeLivreur = codeLivreur.Trim()
            });
    }

    /// <summary>
    /// Renvoie uniquement les tournées disponibles pour un jour.
    /// Cette méthode est volontairement légère : elle ne renvoie pas les clients.
    /// </summary>
    public async Task<IEnumerable<TourneeDisponibleRecord>> GetTourneesDisponiblesAsync(
        DateOnly dateTournee)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var jourTournee = GetJourTournee(dateTournee);

        const string sql = """
            SELECT
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) AS CodeTournee,
                MAX(t.TOURNEE_DESC) AS LibelleTournee,
                COUNT(1) AS NombrePoints
            FROM v_tournee AS t
            WHERE TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) = @JourTournee
              AND t.TOURNEE IS NOT NULL
              AND LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) <> ''
            GROUP BY
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))))
            ORDER BY
                TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))))),
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))));
            """;

        return await connection.QueryAsync<TourneeDisponibleRecord>(
            sql,
            new
            {
                JourTournee = jourTournee
            });
    }

    public async Task<IReadOnlyList<ArticleSaisissableRecord>> GetArticlesSaisissablesAsync()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT
                CodeArticle,
                LibelleArticle,
                OrdreAffichage
            FROM dbo.Mobile_ArticleSaisissable
            WHERE EstActif = 1
              AND EstVisibleMobile = 1
            ORDER BY
                OrdreAffichage,
                CodeArticle;
            """;

        var articles = await connection.QueryAsync<ArticleSaisissableRecord>(sql);
        return articles.ToList();
    }

    public async Task<IReadOnlyList<CommentaireExceptionnelRecord>> GetCommentairesExceptionnelsAsync(
        DateOnly dateTournee,
        string codeTournee)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT
                NULLIF(LTRIM(RTRIM(IdLigneSource)), '') AS IdLigneSource,
                NULLIF(LTRIM(RTRIM(CodeTournee)), '') AS CodeTournee,
                LTRIM(RTRIM(NumClient)) AS NumClient,
                NULLIF(LTRIM(RTRIM(CodePDL)), '') AS CodePDL,
                Commentaire
            FROM dbo.Mobile_CommentaireExceptionnel
            WHERE DateTournee = @DateTournee
              AND (
                    CodeTournee = @CodeTournee
                    OR CodeTournee IS NULL
                    OR LTRIM(RTRIM(CodeTournee)) = ''
                  )
              AND Actif = 1;
            """;

        var commentaires = await connection.QueryAsync<CommentaireExceptionnelRecord>(
            sql,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                CodeTournee = codeTournee.Trim()
            });

        return commentaires.ToList();
    }

    public async Task<IReadOnlyList<PreRemplissageQuantiteRecord>> GetPreRemplissagesAsync(
        DateOnly dateTournee,
        string codeTournee)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        const string sql = """
            SELECT
                q.IdLigneSource,
                q.NumClient,
                q.CodePDL,
                q.CodeArticle,
                COALESCE(q.LibelleArticle, a.LibelleArticle) AS LibelleArticle,
                q.QuantiteLivreePrevue
            FROM dbo.Mobile_PreRemplissageTournee AS p
            INNER JOIN dbo.Mobile_PreRemplissageQuantite AS q
                ON q.IdPreRemplissageTournee = p.IdPreRemplissageTournee
            LEFT JOIN dbo.Mobile_ArticleSaisissable AS a
                ON a.CodeArticle = q.CodeArticle
            WHERE p.DateTournee = @DateTournee
              AND p.CodeTournee = @CodeTournee
              AND p.EstVerrouille = 1
              AND q.Actif = 1;
            """;

        var preRemplissages = await connection.QueryAsync<PreRemplissageQuantiteRecord>(
            sql,
            new
            {
                DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                CodeTournee = codeTournee.Trim()
            });

        return preRemplissages.ToList();
    }

    public async Task SaveChargementTourneeAsync(
        DateOnly dateTournee,
        string schemaVersion,
        LivreurRecord livreur,
        string codeTournee,
        string? libelleTournee,
        int nombrePointsEnvoyes,
        string? nomAppareil = null,
        string? versionApplication = null,
        string? adresseIp = null)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();
        var now = DateTimeOffset.Now;
        var codeLivreur = livreur.CodeLivreur.Trim();
        var nomLivreur = string.IsNullOrWhiteSpace(livreur.NomLivreur)
            ? codeLivreur
            : livreur.NomLivreur.Trim();

        try
        {
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

            await connection.ExecuteAsync(
                """
                INSERT INTO dbo.Mobile_ChargementTournee (
                    SchemaVersion,
                    DateTournee,
                    CodeTournee,
                    LibelleTournee,
                    IdLivreur,
                    DateChargement,
                    NombrePointsEnvoyes,
                    NomAppareil,
                    VersionApplication,
                    AdresseIP,
                    DateCreation
                )
                VALUES (
                    @SchemaVersion,
                    @DateTournee,
                    @CodeTournee,
                    @LibelleTournee,
                    @IdLivreur,
                    @DateChargement,
                    @NombrePointsEnvoyes,
                    @NomAppareil,
                    @VersionApplication,
                    @AdresseIP,
                    @DateCreation
                );
                """,
                new
                {
                    SchemaVersion = schemaVersion,
                    DateTournee = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                    CodeTournee = codeTournee.Trim(),
                    LibelleTournee = libelleTournee,
                    IdLivreur = idLivreur,
                    DateChargement = now,
                    NombrePointsEnvoyes = nombrePointsEnvoyes,
                    NomAppareil = nomAppareil,
                    VersionApplication = versionApplication,
                    AdresseIP = adresseIp,
                    DateCreation = now
                },
                transaction);

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
                    NULL,
                    @IdLivreur,
                    NULL,
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
                    IdLivreur = idLivreur,
                    DateEvenement = now,
                    TypeEvenement = "CHARGEMENT_TOURNEE",
                    Niveau = "INFO",
                    Message = "Tournée chargée par le mobile.",
                    DetailTechnique = $"Tournée {codeTournee.Trim()} du {dateTournee:yyyy-MM-dd} chargée avec {nombrePointsEnvoyes} ligne(s).",
                    AdresseIP = adresseIp,
                    NomAppareil = nomAppareil,
                    VersionApplication = versionApplication
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

    private async Task<string> GetFermetureCustomerColumnAsync(IDbConnection connection)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('v_fermeture')
              AND name = 'CUSTOMERNUMBER';
            """;

        var hasCustomerNumber = await connection.ExecuteScalarAsync<int>(sql);

        return hasCustomerNumber > 0
            ? "CUSTOMERNUMBER"
            : "CUSTOMERNUBLER";
    }

    public async Task<IEnumerable<TourneeLigneRecord>> GetTourneeLinesAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var jourTournee = GetJourTournee(dateTournee);
        var fermetureCustomerColumn = await GetFermetureCustomerColumnAsync(connection);

        var sql = $"""
                WITH TourneeDedoublonnee AS (
                    SELECT
                        t.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50))))
                            ORDER BY
                                TRY_CONVERT(INT, t.ARRET),
                                LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50))))
                        ) AS RowNum
                    FROM v_tournee AS t
                    WHERE TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) = @JourTournee
                      AND (
                            @CodeTournee IS NULL
                            OR LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) = @CodeTournee
                          )
                ),
                PdlDedoublonnee AS (
                    SELECT
                        p.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50))))
                            ORDER BY
                                LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50)))),
                                TRY_CONVERT(INT, LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50)))))
                        ) AS RowNum
                    FROM v_pdl_jour AS p
                )
                SELECT
                    LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) AS NumClient,
                    t.NOM_CLI AS NomClient,

                    t.PDL_DESC AS NomAffiche,

                    LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))) AS CodePDL,
                    t.PDL_DESC AS DescriptionPDL,

                    p.STREET AS AdresseLigne1,
                    p.STREET2 AS AdresseLigne2,
                    p.STREET3 AS AdresseLigne3,
                    p.CITY AS Ville,
                    p.ZIPCODE AS CodePostal,

                    TRY_CONVERT(INT, t.ARRET) AS OrdreArret,
                    TRY_CONVERT(INT, t.ARRET) AS Horaire,

                    TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) AS JourTournee,

                    LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) AS CodeTournee,
                    t.TOURNEE_DESC AS LibelleTournee,

                    TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE_RETOUR AS NVARCHAR(50))))) AS JourTourneeRetour,
                    LTRIM(RTRIM(CAST(t.TOURNEE_RET AS NVARCHAR(50)))) AS CodeTourneeRetour,
                    t.TOURNEE_RETOUR_DESC AS LibelleTourneeRetour,

                    t.SCHEMA_LIV AS SchemaLivraison,

                    t.INSTRUCTIONS AS Instructions,

                    t.ZONE_DECH AS ZoneDechargement,
                    CAST(NULL AS NVARCHAR(100)) AS Zone,

                    CAST(NULL AS NVARCHAR(100)) AS Precision,
                    CAST(NULL AS NVARCHAR(100)) AS Cle,

                    CASE
                        WHEN UPPER(LTRIM(RTRIM(CAST(f.ACTIVE AS NVARCHAR(20))))) IN ('1', 'Y', 'YES', 'O', 'OUI', 'TRUE', 'VRAI')
                            THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS EstFerme,

                    TRY_CONVERT(DATETIME, f.NONBUSINESSDAY) AS DateFermeture,
                    CAST(NULL AS NVARCHAR(255)) AS MotifFermeture
                FROM TourneeDedoublonnee AS t
                LEFT JOIN PdlDedoublonnee AS p
                    ON LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50))))
                   AND LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50))))
                   AND TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50)))))
                        = TRY_CONVERT(INT, LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50)))))
                   AND p.RowNum = 1
                LEFT JOIN v_fermeture AS f
                    ON LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(f.{fermetureCustomerColumn} AS NVARCHAR(50))))
                   AND TRY_CONVERT(DATE, f.NONBUSINESSDAY) = @DateFermeture
                WHERE t.RowNum = 1
                ORDER BY
                    TRY_CONVERT(INT, t.ARRET),
                    LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50)))),
                    LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                    LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50))));
                """;

        return await connection.QueryAsync<TourneeLigneRecord>(
            sql,
            new
            {
                DateFermeture = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
                JourTournee = jourTournee,
                CodeTournee = string.IsNullOrWhiteSpace(codeTournee)
                    ? null
                    : codeTournee.Trim()
            });
    }

    public static int GetJourTournee(DateOnly dateTournee)
    {
        return dateTournee.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(dateTournee))
        };
    }
}
