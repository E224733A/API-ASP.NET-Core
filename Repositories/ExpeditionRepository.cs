using System.Globalization;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;
using Dapper;

namespace API_ASP.NET_Core.Repositories;

/// <summary>
/// Repository SQL du module Expédition.
/// </summary>
/// <remarks>
/// Ce repository persiste le verrouillage global des préparations Expédition dans les tables Mobile_*.
/// Les opérations principales sont transactionnelles : un lot, ses préparations, ses lignes,
/// ses commentaires exceptionnels, son historique et son log doivent rester cohérents.
/// Modifier ce fichier peut impacter directement le verrouillage automatique du soir.
/// </remarks>
public sealed class ExpeditionRepository
{
    private const string ParisTimeZoneIanaId = "Europe/Paris";
    private const string ParisTimeZoneWindowsId = "Romance Standard Time";

    private readonly SqlConnectionFactory _connectionFactory;

    public ExpeditionRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

/// <summary>
/// Recherche un lot de verrouillage Expédition déjà enregistré à partir de son identifiant technique.
/// </summary>
/// <remarks>
/// Cette lecture sert à détecter un lot déjà reçu et à rattacher le lot global
/// à une préparation existante lorsque la base contient déjà des données pour ce verrouillage.
/// </remarks>
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

/// <summary>
/// Récupère l'état de verrouillage d'une préparation Expédition pour une date et une tournée.
/// </summary>
/// <remarks>
/// Cette méthode permet de savoir si une tournée est déjà préparée, verrouillée ou rattachée
/// à un lot de verrouillage avant de traiter un nouveau payload Expédition.
/// </remarks>
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

/// <summary>
/// Enregistre transactionnellement un lot global de verrouillage Expédition.
/// </summary>
/// <remarks>
/// Règle métier : le verrouillage Expédition remplace le lot global actif de la même date,
/// puis sauvegarde l'état complet des préparations envoyées par ServeWeb.
/// Toute la sauvegarde est atomique : lot, préparations, lignes, commentaires, historique
/// et log sont validés ensemble ou annulés ensemble.
/// </remarks>
    public async Task<ExpeditionVerrouillageLotSaveResult> EnregistrerVerrouillageLotAsync(
        ExpeditionVerrouillageLotCommand command,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string empreintePayload,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync(cancellationToken);

        // Transaction critique : un verrouillage partiel rendrait le diagnostic Expédition incohérent.
        using var transaction = connection.BeginTransaction();

        var now = GetNowParis();
        var nombreTournees = command.Tournees.Count;
        var nombreLignes = command.Tournees.Sum(tournee => tournee.Lignes.Count);
        var nombreQuantites = command.Tournees
            .SelectMany(tournee => tournee.Lignes)
            .SelectMany(ligne => ligne.QuantitesPrevues)
            .Count(quantite => quantite.QuantiteLivreePrevue.HasValue);

        try
        {
            // Règle métier : un nouveau verrouillage global remplace l'ancien lot VERROUILLE de la même date.
            await RemplacerAncienLotGlobalVerrouilleAsync(
                connection,
                transaction,
                dateTournee,
                idLotVerrouillageTechnique,
                now,
                cancellationToken);

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
                        LibelleTournee = $"Lot global Expédition {command.IdLotVerrouillage}",
                        NombrePreparations = nombreTournees,
                        NombreLignes = nombreLignes,
                        NombreQuantites = nombreQuantites,
                        AdresseIP = NormalizeNullable(adresseIp),
                        MessageRetour = "Préparations Expédition verrouillées.",
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            foreach (var tournee in command.Tournees)
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

                // Les anciennes lignes sont désactivées logiquement pour conserver l'historique SQL.
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
                        command,
                        tournee,
                        ligne,
                        dateTournee,
                        now,
                        cancellationToken);
                }

                await EnregistrerHistoriqueTourneeAsync(
                    connection,
                    transaction,
                    command,
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
                command,
                dateTournee,
                idLotVerrouillageTechnique,
                empreintePayload,
                adresseIp,
                now,
                cancellationToken);

            // Validation finale uniquement après écriture complète du lot, des lignes, des commentaires et du log.
            transaction.Commit();

            return new ExpeditionVerrouillageLotSaveResult
            {
                NombreTourneesVerrouillees = nombreTournees,
                NombreLignesVerrouillees = nombreLignes,
                DateReceptionApi = now,
                DateSauvegardeSql = now
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

/// <summary>
/// Remplace logiquement l'ancien lot global verrouillé pour la même date de tournée.
/// </summary>
/// <remarks>
/// Un seul lot global doit rester actif pour une date Expédition. Les anciens lots ne sont pas
/// supprimés : ils passent au statut REMPLACE pour conserver une trace d'audit.
/// </remarks>
    private static async Task RemplacerAncienLotGlobalVerrouilleAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE dbo.Mobile_ExpeditionLotVerrouillage
                SET
                    StatutLot = N'REMPLACE',
                    DateModification = @Now,
                    MessageRetour = N'Lot remplacé par un nouveau verrouillage Expédition.'
                WHERE DateTournee = @DateTournee
                  AND CodeTournee = N'GLOBAL'
                  AND StatutLot = N'VERROUILLE'
                  AND IdLotVerrouillage <> @IdLotVerrouillage;
                """,
                new
                {
                    DateTournee = dateTournee.Date,
                    IdLotVerrouillage = idLotVerrouillageTechnique,
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

/// <summary>
/// Crée ou met à jour la préparation Expédition d'une tournée dans le lot courant.
/// </summary>
/// <remarks>
/// L'upsert est verrouillé côté SQL pour éviter deux préparations concurrentes sur la même
/// date et le même code tournée. La DateModification reçue de ServeWeb est conservée quand
/// elle est exploitable, sinon l'heure serveur Paris est utilisée.
/// </remarks>    
    private static async Task<long> UpsertPreparationExpeditionAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageTourneeCommand tournee,
        DateTime dateTournee,
        Guid idLotVerrouillageTechnique,
        string empreintePayload,
        string? adresseIp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Date métier : conserver l'heure du clic "prêt verrouillage" envoyée par ServeWeb si elle est valide.
        var dateModificationPreparation = TryParseDateModificationParis(tournee.DateModification) ?? now;

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
                        @DateModificationPreparation
                    );

                    SET @IdPreparationExpedition = CONVERT(BIGINT, SCOPE_IDENTITY());
                END
                ELSE
                BEGIN
                    UPDATE dbo.Mobile_ExpeditionPreparation
                    SET
                        LibelleTournee = @LibelleTournee,
                        StatutPreparation = N'VERROUILLEE',
                        EstVerrouille = 1,
                        DateVerrouillage = @Now,
                        IdLotVerrouillage = @IdLotVerrouillage,
                        EmpreintePayload = @EmpreintePayload,
                        AdresseIPVerrouillage = @AdresseIP,
                        DateModification = @DateModificationPreparation
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
                    DateModificationPreparation = dateModificationPreparation,
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

/// <summary>
/// Désactive les anciennes lignes de préparation avant d'insérer les quantités du nouveau lot.
/// </summary>
/// <remarks>
/// La base conserve les anciennes lignes pour audit, mais seules les lignes Actif = 1
/// représentent l'état courant de la préparation Expédition.
/// </remarks>
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

/// <summary>
/// Enregistre les quantités prévues d'une ligne Expédition dans les lignes actives de préparation.
/// </summary>
/// <remarks>
/// Seules les quantités réellement prévues sont persistées. Une quantité absente dans le payload
/// signifie "non préparée" et ne doit pas créer de ligne SQL vide.
/// </remarks>
    private static async Task EnregistrerQuantitesLigneAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        long idPreparationExpedition,
        ExpeditionVerrouillageLigneCommand ligne,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (ligne.QuantitesPrevues.Count == 0)
        {
            return;
        }

        foreach (var quantite in ligne.QuantitesPrevues)
        {
            // Contrat JSON : une quantité prévue absente ne doit pas devenir une quantité SQL à 0.
            if (!quantite.QuantiteLivreePrevue.HasValue)
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
                        CodePDL = NormalizeNullable(ligne.PointLivraison.CodePDL),
                        DescriptionPDL = NormalizeNullable(ligne.PointLivraison.DescriptionPDL),
                        CodeArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                        LibelleArticle = quantite.CodeArticle.Trim().ToUpperInvariant(),
                        QuantiteLivreePrevue = quantite.QuantiteLivreePrevue.Value,
                        Now = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

/// <summary>
/// Met à jour le commentaire exceptionnel associé à une ligne de préparation Expédition.
/// </summary>
/// <remarks>
/// Règle métier : null signifie "ne pas modifier le commentaire existant", tandis qu'une chaîne
/// vide signifie "désactiver le commentaire actif". Un texte non vide remplace le commentaire actif.
/// </remarks>
    private static async Task EnregistrerCommentaireLigneAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotCommand command,
        ExpeditionVerrouillageTourneeCommand tournee,
        ExpeditionVerrouillageLigneCommand ligne,
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
                    CodePDL = NormalizeNullable(ligne.PointLivraison.CodePDL),
                    Commentaire = ligne.CommentaireExceptionnel.Trim(),
                    CreePar = NormalizeNullable(ligne.DerniereModification?.Utilisateur) ?? NormalizeNullable(command.Source),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

/// <summary>
/// Ajoute une entrée d'historique pour tracer le verrouillage d'une tournée Expédition.
/// </summary>
/// <remarks>
/// L'historique permet de relier une préparation verrouillée au lot métier reçu depuis ServeWeb
/// et à l'identifiant technique généré côté API.
/// </remarks>
    private static async Task EnregistrerHistoriqueTourneeAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotCommand command,
        ExpeditionVerrouillageTourneeCommand tournee,
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
                    Commentaire = $"Lot global Expédition verrouillé : {command.IdLotVerrouillage} ({idLotVerrouillageTechnique:D})",
                    AdresseIP = NormalizeNullable(adresseIp),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }

/// <summary>
/// Écrit le log technique global du verrouillage Expédition.
/// </summary>
/// <remarks>
/// Ce log sert au diagnostic d'exploitation : il conserve la date tournée, l'identifiant métier,
/// l'identifiant technique du lot et l'empreinte du payload reçu.
/// </remarks>
    private static async Task EnregistrerLogVerrouillageAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction,
        ExpeditionVerrouillageLotCommand command,
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
                    DetailTechnique = $"DateTournee={dateTournee:yyyy-MM-dd}; IdLotVerrouillageMetier={command.IdLotVerrouillage}; IdLotVerrouillageTechnique={idLotVerrouillageTechnique:D}; EmpreintePayload={empreintePayload}",
                    AdresseIP = NormalizeNullable(adresseIp),
                    Now = now
                },
                transaction,
                cancellationToken: cancellationToken));
    }


// Retourne l'heure serveur convertie dans le fuseau métier Europe/Paris.
    private static DateTimeOffset GetNowParis()
    {
        return ToParisOffset(DateTimeOffset.UtcNow);
    }

// Convertit une date horodatée vers le fuseau métier Europe/Paris.
    private static DateTimeOffset ToParisOffset(DateTimeOffset value)
    {
        return TimeZoneInfo.ConvertTime(value, GetParisTimeZone());
    }

/// <summary>
/// Tente de convertir la date de modification envoyée par ServeWeb dans le fuseau métier Paris.
/// </summary>
/// <remarks>
/// Cette date représente l'heure fonctionnelle du clic "prêt verrouillage".
/// Si elle est absente ou invalide, l'appelant utilise l'heure serveur.
/// </remarks>
    private static DateTimeOffset? TryParseDateModificationParis(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateModification)
            ? ToParisOffset(dateModification)
            : null;
    }

/// <summary>
/// Résout le fuseau Paris selon l'identifiant disponible sur l'environnement d'exécution.
/// </summary>
/// <remarks>
/// IIS Windows utilise "Romance Standard Time", tandis que certains environnements utilisent
/// l'identifiant IANA "Europe/Paris". Le fallback évite de changer la règle métier selon l'OS.
/// </remarks>
    private static TimeZoneInfo GetParisTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ParisTimeZoneIanaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ParisTimeZoneWindowsId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ParisTimeZoneWindowsId);
        }
    }

/// <summary>
/// Normalise les chaînes optionnelles avant persistance SQL.
/// </summary>
/// <remarks>
/// Les chaînes vides sont stockées comme NULL afin d'éviter de distinguer artificiellement
/// une absence de valeur et un texte vide dans les diagnostics SQL.
/// </remarks>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
