using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Validators;
using Microsoft.Data.SqlClient;
using System.Data;

namespace API_ASP.NET_Core.Repositories;

/// <summary>
/// Repository SQL responsable de l'enregistrement final des tournées envoyées par le mobile.
/// </summary>
/// <remarks>
/// Ce repository écrit dans les tables Mobile_* après validation du contrat JSON par le service.
/// Il centralise la persistance transactionnelle de la tournée, du livreur, du trajet camion,
/// des lignes, des quantités et des logs. Les contrôles de doublon s'appuient sur
/// IdSynchronisation et sur le couple DateTournee + CodeTournee.
/// </remarks>
public sealed class SynchronisationsRepository
{
    private const string CodeArticleRollsVides = "ROLLS_VIDES";
    private const string LibelleArticleRollsVides = "Chariots vides";

    private readonly string _connectionString;

    public SynchronisationsRepository(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("MobileConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("La chaîne de connexion MobileConnection est introuvable.");
    }

/// <summary>
/// Recherche une synchronisation déjà reçue avec le même IdSynchronisation.
/// </summary>
/// <remarks>
/// Ce contrôle protège l'idempotence technique : si le mobile renvoie le même identifiant
/// après un retry réseau, l'API peut reconnaître l'envoi déjà enregistré.
/// </remarks>
    public async Task<SynchronisationDejaRecueDto?> GetSynchronisationDejaRecueAsync(
        Guid idSynchronisation,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (1)
    t.IdTourneeMobile,
    t.IdSynchronisation,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.DateEnvoi,
    t.DateReceptionApi
FROM dbo.Mobile_Tournee t
LEFT JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
WHERE t.IdSynchronisation = @IdSynchronisation
ORDER BY t.DateReceptionApi ASC, t.IdTourneeMobile ASC;
";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdSynchronisation", SqlDbType.UniqueIdentifier).Value = idSynchronisation;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SynchronisationDejaRecueDto
        {
            IdTourneeMobile = reader.GetInt64(reader.GetOrdinal("IdTourneeMobile")),
            IdSynchronisation = reader.GetGuid(reader.GetOrdinal("IdSynchronisation")),
            DateTournee = reader.GetDateTime(reader.GetOrdinal("DateTournee")),
            CodeTournee = reader.GetString(reader.GetOrdinal("CodeTournee")),
            LibelleTournee = ReadNullableString(reader, "LibelleTournee"),
            CodeLivreur = ReadNullableString(reader, "CodeLivreur"),
            NomLivreur = ReadNullableString(reader, "NomLivreur"),
            DateEnvoi = ReadNullableDateTimeOffset(reader, "DateEnvoi"),
            DateReceptionApi = ReadNullableDateTimeOffset(reader, "DateReceptionApi")
        };
    }

/// <summary>
/// Recherche une tournée déjà envoyée pour la même date métier et le même code tournée.
/// </summary>
/// <remarks>
/// Ce contrôle protège le doublon métier : deux IdSynchronisation différents ne doivent pas
/// permettre d'enregistrer deux fois la même tournée finalisée.
/// </remarks>
    public async Task<TourneeDejaEnvoyeeDto?> GetTourneeDejaEnvoyeeAsync(
        DateTime dateTournee,
        string codeTournee,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP (1)
    t.IdTourneeMobile,
    t.DateTournee,
    t.CodeTournee,
    t.LibelleTournee,
    l.CodeLivreur,
    l.NomLivreur,
    t.DateEnvoi,
    t.DateReceptionApi
FROM dbo.Mobile_Tournee t
INNER JOIN dbo.Mobile_Livreur l
    ON l.IdLivreur = t.IdLivreur
WHERE t.DateTournee = @DateTournee
  AND t.CodeTournee = @CodeTournee
  AND t.StatutSynchronisation = N'ENVOYEE'
ORDER BY t.DateReceptionApi ASC, t.IdTourneeMobile ASC;
";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@DateTournee", SqlDbType.Date).Value = dateTournee.Date;
        command.Parameters.Add("@CodeTournee", SqlDbType.NVarChar, 50).Value = codeTournee.Trim();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TourneeDejaEnvoyeeDto
        {
            IdTourneeMobile = reader.GetInt64(reader.GetOrdinal("IdTourneeMobile")),
            DateTournee = reader.GetDateTime(reader.GetOrdinal("DateTournee")),
            CodeTournee = reader.GetString(reader.GetOrdinal("CodeTournee")),
            LibelleTournee = ReadNullableString(reader, "LibelleTournee"),
            CodeLivreur = reader.GetString(reader.GetOrdinal("CodeLivreur")),
            NomLivreur = ReadNullableString(reader, "NomLivreur"),
            DateEnvoi = ReadNullableDateTimeOffset(reader, "DateEnvoi"),
            DateReceptionApi = ReadNullableDateTimeOffset(reader, "DateReceptionApi")
        };
    }

/// <summary>
/// Enregistre transactionnellement le bilan final d'une tournée mobile.
/// </summary>
/// <remarks>
/// À ce stade, le payload a déjà été validé par le service et le validator.
/// La transaction garantit que l'en-tête de tournée, le livreur, le trajet camion,
/// les lignes, les quantités et le log restent cohérents. En cas d'erreur,
/// aucune partie de la synchronisation ne doit rester enregistrée seule.
/// </remarks>
    public async Task<SynchronisationEnregistrementResult> EnregistrerSynchronisationAsync(
        SynchronisationTourneeRequest request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var dateTournee = SynchronisationTourneeValidator.ParseDateTournee(request.DateTournee);
        var idSynchronisation = ParseGuid(request.IdSynchronisation);
        var livreur = request.Livreur
            ?? throw new InvalidOperationException("Le livreur a été validé mais reste null.");
        var mobile = request.Mobile
            ?? throw new InvalidOperationException("Les informations mobile ont été validées mais restent null.");
        var trajet = request.Trajet
            ?? throw new InvalidOperationException("Le trajet a été validé mais reste null.");
        var lignes = request.Lignes
            ?? throw new InvalidOperationException("Les lignes ont été validées mais restent null.");
        var dateChargementMobile = ParseDateTimeOffsetOrNull(mobile.DateChargementMobile);
        var dateEnvoiMobile = ParseDateTimeOffsetOrNull(mobile.DateEnvoiMobile) ?? DateTimeOffset.Now;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Transaction critique : une tournée mobile ne doit jamais être enregistrée partiellement.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Référentiel mobile : le livreur reçu est synchronisé dans Mobile_Livreur avant la tournée.
            var idLivreur = await GetOrCreateLivreurAsync(
                connection,
                (SqlTransaction)transaction,
                livreur.CodeLivreur,
                livreur.NomLivreur,
                cancellationToken);

            var idTourneeMobile = await InsertTourneeAsync(
                connection,
                (SqlTransaction)transaction,
                request,
                idSynchronisation,
                dateTournee,
                idLivreur,
                dateChargementMobile,
                dateEnvoiMobile,
                adresseIp,
                cancellationToken);

            // Contrat 1.3 : le trajet camion est obligatoire dans l'envoi final mobile.
            await InsertTourneeCamionAsync(
                connection,
                (SqlTransaction)transaction,
                idTourneeMobile,
                trajet,
                cancellationToken);

            var nombreLignes = 0;
            var nombreQuantites = 0;

            foreach (var ligne in lignes)
            {
                var idTourneeLigne = await InsertLigneAsync(
                    connection,
                    (SqlTransaction)transaction,
                    request,
                    ligne,
                    idTourneeMobile,
                    cancellationToken);

                nombreLignes++;

                var saisie = ligne.Saisie
                    ?? throw new InvalidOperationException("La saisie d'une ligne a été validée mais reste null.");

                // Normalisation avant persistance : les codes articles doivent être stables en base.
                var quantitesLigne = NormalizeQuantites(
                    saisie.Quantites
                    ?? throw new InvalidOperationException("Les quantités d'une ligne ont été validées mais restent null."));

                foreach (var quantite in quantitesLigne)
                {
                    await EnsureArticleAsync(
                        connection,
                        (SqlTransaction)transaction,
                        quantite.CodeArticle,
                        quantite.Libelle,
                        cancellationToken);

                    await InsertQuantiteAsync(
                        connection,
                        (SqlTransaction)transaction,
                        idTourneeLigne,
                        quantite,
                        cancellationToken);

                    nombreQuantites++;
                }
            }

            await InsertLogAsync(
                connection,
                (SqlTransaction)transaction,
                idTourneeMobile,
                idLivreur,
                idSynchronisation,
                "ENVOI_REUSSI",
                "INFO",
                "Synchronisation enregistrée avec succès.",
                null,
                adresseIp,
                request.Mobile?.NomAppareil,
                request.Mobile?.VersionApplication,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new SynchronisationEnregistrementResult
            {
                IdTourneeMobile = idTourneeMobile,
                IdSynchronisation = idSynchronisation,
                DateReceptionApi = DateTimeOffset.Now,
                NombreLignesRecues = nombreLignes,
                NombreQuantitesRecues = nombreQuantites
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

/// <summary>
/// Trace une tentative de double envoi refusée par les contrôles métier.
/// </summary>
/// <remarks>
/// Aucun nouvel enregistrement de tournée n'est créé ici. Le log sert uniquement à expliquer
/// pourquoi le POST mobile a été rejeté alors qu'une tournée existe déjà pour la même date
/// et le même code tournée.
/// </remarks>
    public async Task EcrireLogDoubleEnvoiAsync(
        SynchronisationTourneeRequest request,
        TourneeDejaEnvoyeeDto tourneeExistante,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var idLivreur = await GetLivreurIdOrNullAsync(
            connection,
            request.Livreur?.CodeLivreur,
            cancellationToken);

        var idSynchronisation = SynchronisationTourneeValidator.TryParseGuid(
            request.IdSynchronisation,
            out var parsedGuid)
            ? parsedGuid
            : (Guid?)null;

        const string sql = @"
INSERT INTO dbo.Mobile_LogSynchronisation
    (
        IdTourneeMobile,
        IdLivreur,
        IdSynchronisation,
        TypeEvenement,
        Niveau,
        Message,
        DetailTechnique,
        AdresseIP,
        NomAppareil,
        VersionApplication
    )
VALUES
    (
        @IdTourneeMobile,
        @IdLivreur,
        @IdSynchronisation,
        N'DOUBLE_ENVOI',
        N'WARNING',
        @Message,
        @DetailTechnique,
        @AdresseIP,
        @NomAppareil,
        @VersionApplication
    );
";

        var detailTechnique =
            $"Tentative de double envoi pour DateTournee={request.DateTournee}, CodeTournee={request.CodeTournee}. " +
            $"Tournee existante IdTourneeMobile={tourneeExistante.IdTourneeMobile}, CodeLivreur={tourneeExistante.CodeLivreur}.";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IdTourneeMobile", SqlDbType.BigInt).Value = tourneeExistante.IdTourneeMobile;
        command.Parameters.Add("@IdLivreur", SqlDbType.Int).Value = ToDbValue(idLivreur);
        command.Parameters.Add("@IdSynchronisation", SqlDbType.UniqueIdentifier).Value = ToDbValue(idSynchronisation);
        command.Parameters.Add("@Message", SqlDbType.NVarChar, 1000).Value = "Double envoi bloqué : tournée déjà envoyée pour cette date.";
        command.Parameters.Add("@DetailTechnique", SqlDbType.NVarChar).Value = detailTechnique;
        command.Parameters.Add("@AdresseIP", SqlDbType.NVarChar, 50).Value = ToDbValue(adresseIp);
        command.Parameters.Add("@NomAppareil", SqlDbType.NVarChar, 100).Value = ToDbValue(request.Mobile?.NomAppareil);
        command.Parameters.Add("@VersionApplication", SqlDbType.NVarChar, 50).Value = ToDbValue(request.Mobile?.VersionApplication);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

/// <summary>
/// Crée ou met à jour le livreur mobile utilisé par la synchronisation.
/// </summary>
/// <remarks>
/// Le verrou SQL évite de créer deux lignes Mobile_Livreur pour le même code livreur
/// si deux synchronisations arrivent en même temps.
/// </remarks>
    private async Task<int> GetOrCreateLivreurAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string codeLivreur,
        string? nomLivreur,
        CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @IdLivreur INT;

SELECT @IdLivreur = IdLivreur
FROM dbo.Mobile_Livreur WITH (UPDLOCK, HOLDLOCK)
WHERE CodeLivreur = @CodeLivreur;

IF @IdLivreur IS NULL
BEGIN
    INSERT INTO dbo.Mobile_Livreur
        (CodeLivreur, NomLivreur)
    VALUES
        (@CodeLivreur, @NomLivreur);

    SET @IdLivreur = CONVERT(INT, SCOPE_IDENTITY());
END
ELSE
BEGIN
    UPDATE dbo.Mobile_Livreur
    SET
        NomLivreur = @NomLivreur,
        EstActif = 1,
        DateModification = SYSDATETIMEOFFSET()
    WHERE IdLivreur = @IdLivreur;
END

SELECT @IdLivreur;
";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CodeLivreur", SqlDbType.NVarChar, 50).Value = codeLivreur.Trim();
        command.Parameters.Add("@NomLivreur", SqlDbType.NVarChar, 100).Value =
            string.IsNullOrWhiteSpace(nomLivreur) ? codeLivreur.Trim() : nomLivreur.Trim();

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

/// <summary>
/// Retrouve l'identifiant SQL du livreur si le code est exploitable.
/// </summary>
/// <remarks>
/// Cette méthode est utilisée pour enrichir les logs sans bloquer l'écriture du diagnostic
/// lorsqu'aucun livreur ne peut être rattaché proprement.
/// </remarks>
    private async Task<int?> GetLivreurIdOrNullAsync(
        SqlConnection connection,
        string? codeLivreur,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        const string sql = @"
SELECT TOP (1) IdLivreur
FROM dbo.Mobile_Livreur
WHERE CodeLivreur = @CodeLivreur;
";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CodeLivreur", SqlDbType.NVarChar, 50).Value = codeLivreur.Trim();

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? null : Convert.ToInt32(result);
    }

/// <summary>
/// Insère l'en-tête de tournée mobile finalisée.
/// </summary>
/// <remarks>
/// Cette ligne porte l'identité de la synchronisation, la date métier, le livreur,
/// le statut ENVOYEE et les informations appareil. Les lignes et quantités sont ensuite
/// rattachées à l'identifiant généré.
/// </remarks>
    private async Task<long> InsertTourneeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SynchronisationTourneeRequest request,
        Guid idSynchronisation,
        DateTime dateTournee,
        int idLivreur,
        DateTimeOffset? dateChargementMobile,
        DateTimeOffset dateEnvoiMobile,
        string? adresseIp,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.Mobile_Tournee
    (
        SchemaVersion,
        IdSynchronisation,
        DateTournee,
        CodeTournee,
        LibelleTournee,
        IdLivreur,
        StatutSynchronisation,
        DateChargementMobile,
        DateEnvoi,
        EstVerrouillee,
        NombrePointsPrevus,
        NombrePointsSaisis,
        CommentaireGlobal,
        NomAppareil,
        VersionApplication,
        AdresseIP
    )
OUTPUT INSERTED.IdTourneeMobile
VALUES
    (
        @SchemaVersion,
        @IdSynchronisation,
        @DateTournee,
        @CodeTournee,
        @LibelleTournee,
        @IdLivreur,
        N'ENVOYEE',
        @DateChargementMobile,
        @DateEnvoi,
        1,
        @NombrePointsPrevus,
        @NombrePointsSaisis,
        @CommentaireGlobal,
        @NomAppareil,
        @VersionApplication,
        @AdresseIP
    );
";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@SchemaVersion", SqlDbType.NVarChar, 20).Value = request.SchemaVersion.Trim();
        command.Parameters.Add("@IdSynchronisation", SqlDbType.UniqueIdentifier).Value = idSynchronisation;
        command.Parameters.Add("@DateTournee", SqlDbType.Date).Value = dateTournee.Date;
        command.Parameters.Add("@CodeTournee", SqlDbType.NVarChar, 50).Value = request.CodeTournee.Trim();
        command.Parameters.Add("@LibelleTournee", SqlDbType.NVarChar, 255).Value = ToDbValue(request.LibelleTournee);
        command.Parameters.Add("@IdLivreur", SqlDbType.Int).Value = idLivreur;
        command.Parameters.Add("@DateChargementMobile", SqlDbType.DateTimeOffset).Value = ToDbValue(dateChargementMobile);
        command.Parameters.Add("@DateEnvoi", SqlDbType.DateTimeOffset).Value = dateEnvoiMobile;
        var nombreLignes = request.Lignes?.Count ?? 0;
        command.Parameters.Add("@NombrePointsPrevus", SqlDbType.Int).Value = nombreLignes;
        command.Parameters.Add("@NombrePointsSaisis", SqlDbType.Int).Value = nombreLignes;
        command.Parameters.Add("@CommentaireGlobal", SqlDbType.NVarChar, 1000).Value = ToDbValue(request.CommentaireGlobal);
        command.Parameters.Add("@NomAppareil", SqlDbType.NVarChar, 100).Value = ToDbValue(request.Mobile?.NomAppareil);
        command.Parameters.Add("@VersionApplication", SqlDbType.NVarChar, 50).Value = ToDbValue(request.Mobile?.VersionApplication);
        command.Parameters.Add("@AdresseIP", SqlDbType.NVarChar, 50).Value = ToDbValue(adresseIp);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

/// <summary>
/// Enregistre le camion et les informations de trajet déclarés par le mobile.
/// </summary>
/// <remarks>
/// Le trajet camion fait partie du contrat d'envoi final : identifiant camion,
/// kilométrages et horaires mobiles doivent avoir été validés avant l'appel repository.
/// </remarks>
    private async Task InsertTourneeCamionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long idTourneeMobile,
        SynchronisationTrajetRequest trajet,
        CancellationToken cancellationToken)
    {
        var camion = trajet.Camion
            ?? throw new InvalidOperationException("Le camion du trajet a été validé mais reste null.");

        if (string.IsNullOrWhiteSpace(camion.IdCamion))
        {
            throw new InvalidOperationException("L'identifiant camion du trajet a été validé mais reste vide.");
        }

        var kilometrageDepart = trajet.KilometrageDepart
            ?? throw new InvalidOperationException("Le kilométrage de départ a été validé mais reste null.");
        var kilometrageArrivee = trajet.KilometrageArrivee
            ?? throw new InvalidOperationException("Le kilométrage d'arrivée a été validé mais reste null.");
        var dateDepartMobile = ParseDateTimeOffsetOrNull(trajet.DateDepartMobile)
            ?? throw new InvalidOperationException("La date de départ mobile a été validée mais reste invalide.");
        var dateArriveeMobile = ParseDateTimeOffsetOrNull(trajet.DateArriveeMobile)
            ?? throw new InvalidOperationException("La date d'arrivée mobile a été validée mais reste invalide.");

        const string sql = @"
INSERT INTO dbo.Mobile_TourneeCamion
    (
        IdTourneeMobile,
        IdCamionSource,
        CodeCamion,
        LibelleCamion,
        Immatriculation,
        KilometrageDepart,
        KilometrageArrivee,
        DateDepartMobile,
        DateArriveeMobile
    )
VALUES
    (
        @IdTourneeMobile,
        @IdCamionSource,
        @CodeCamion,
        @LibelleCamion,
        @Immatriculation,
        @KilometrageDepart,
        @KilometrageArrivee,
        @DateDepartMobile,
        @DateArriveeMobile
    );
";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdTourneeMobile", SqlDbType.BigInt).Value = idTourneeMobile;
        command.Parameters.Add("@IdCamionSource", SqlDbType.NVarChar, 50).Value = camion.IdCamion.Trim();
        command.Parameters.Add("@CodeCamion", SqlDbType.NVarChar, 50).Value = ToDbValue(camion.CodeCamion);
        command.Parameters.Add("@LibelleCamion", SqlDbType.NVarChar, 150).Value = ToDbValue(camion.LibelleCamion);
        command.Parameters.Add("@Immatriculation", SqlDbType.NVarChar, 50).Value = ToDbValue(camion.Immatriculation);
        command.Parameters.Add("@KilometrageDepart", SqlDbType.Int).Value = kilometrageDepart;
        command.Parameters.Add("@KilometrageArrivee", SqlDbType.Int).Value = kilometrageArrivee;
        command.Parameters.Add("@DateDepartMobile", SqlDbType.DateTimeOffset).Value = dateDepartMobile;
        command.Parameters.Add("@DateArriveeMobile", SqlDbType.DateTimeOffset).Value = dateArriveeMobile;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

/// <summary>
/// Insère une ligne de tournée mobile avec les informations client, point de livraison et saisie livreur.
/// </summary>
/// <remarks>
/// Cette méthode conserve à la fois les données chargées le matin et le résultat saisi par le livreur.
/// Les totaux de quantités sont recalculés depuis le détail des articles pour garder une synthèse
/// exploitable directement dans Mobile_TourneeLigne.
/// </remarks>
    private async Task<long> InsertLigneAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SynchronisationTourneeRequest request,
        SynchronisationLigneRequest ligne,
        long idTourneeMobile,
        CancellationToken cancellationToken)
    {
        // Synthèse SQL : les totaux par ligne sont recalculés depuis les quantités détaillées du payload.
        var quantites = NormalizeQuantites(ligne.Saisie?.Quantites ?? new List<SynchronisationQuantiteRequest>());
        var totalLivre = quantites.Sum(q => q.QuantiteLivree);
        var totalRecupere = quantites.Sum(q => q.QuantiteRecuperee);
        var nbRolls = GetQuantiteLivreeArticle(quantites, "ROLLS");
        var nbTapis = GetQuantiteLivreeArticle(quantites, "TAPIS");
        var nbSacs = GetQuantiteLivreeArticle(quantites, "SACS");

        const string sql = @"
INSERT INTO dbo.Mobile_TourneeLigne
    (
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
        EstFerme,
        DateFermeture,
        MotifFermeture,
        QuantiteLivree,
        QuantiteReprise,
        NbRolls,
        NbTapis,
        NbSacs,
        NbRecuperes,
        PrecisionLivreur,
        StatutPassage,
        CommentaireLivreur,
        HeureValidation,
        EstValidee
    )
OUTPUT INSERTED.IdTourneeLigne
VALUES
    (
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
        @EstFerme,
        @DateFermeture,
        @MotifFermeture,
        @QuantiteLivree,
        @QuantiteReprise,
        @NbRolls,
        @NbTapis,
        @NbSacs,
        @NbRecuperes,
        @PrecisionLivreur,
        @StatutPassage,
        @CommentaireLivreur,
        @HeureValidation,
        @EstValidee
    );
";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdTourneeMobile", SqlDbType.BigInt).Value = idTourneeMobile;
        command.Parameters.Add("@IdLigneSource", SqlDbType.NVarChar, 300).Value = ligne.IdLigneSource.Trim();

        command.Parameters.Add("@NumClient", SqlDbType.NVarChar, 50).Value = ligne.Client?.NumClient?.Trim() ?? string.Empty;
        command.Parameters.Add("@NomClient", SqlDbType.NVarChar, 255).Value = ligne.Client?.NomClient?.Trim() ?? string.Empty;
        command.Parameters.Add("@NomAffiche", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.Client?.NomAffiche);

        command.Parameters.Add("@CodePDL", SqlDbType.NVarChar, 50).Value = ToDbValue(ligne.PointLivraison?.CodePDL);
        command.Parameters.Add("@DescriptionPDL", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.PointLivraison?.DescriptionPDL);
        command.Parameters.Add("@AdresseLigne1", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.PointLivraison?.AdresseLigne1);
        command.Parameters.Add("@AdresseLigne2", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.PointLivraison?.AdresseLigne2);
        command.Parameters.Add("@AdresseLigne3", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.PointLivraison?.AdresseLigne3);
        command.Parameters.Add("@Ville", SqlDbType.NVarChar, 100).Value = ToDbValue(ligne.PointLivraison?.Ville);
        command.Parameters.Add("@CodePostal", SqlDbType.NVarChar, 20).Value = ToDbValue(ligne.PointLivraison?.CodePostal);

        command.Parameters.Add("@CodeTournee", SqlDbType.NVarChar, 50).Value = string.IsNullOrWhiteSpace(ligne.Tournee?.CodeTournee)
            ? request.CodeTournee.Trim()
            : ligne.Tournee.CodeTournee.Trim();
        command.Parameters.Add("@LibelleTournee", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.Tournee?.LibelleTournee ?? request.LibelleTournee);
        command.Parameters.Add("@JourTournee", SqlDbType.Int).Value = ToDbValue(ligne.Tournee?.JourTournee);
        command.Parameters.Add("@JourLibelle", SqlDbType.NVarChar, 30).Value = ToDbValue(ligne.Tournee?.JourLibelle);
        command.Parameters.Add("@SchemaLivraison", SqlDbType.NVarChar, 100).Value = ToDbValue(ligne.Tournee?.SchemaLivraison);
        command.Parameters.Add("@OrdreArret", SqlDbType.Int).Value = ToDbValue(ligne.OrdreArret);
        command.Parameters.Add("@Horaire", SqlDbType.NVarChar, 50).Value = ToDbValue(ligne.Horaire);

        command.Parameters.Add("@JourTourneeRetour", SqlDbType.Int).Value = ToDbValue(ligne.Retour?.JourTourneeRetour);
        command.Parameters.Add("@JourRetourLibelle", SqlDbType.NVarChar, 30).Value = ToDbValue(ligne.Retour?.JourRetourLibelle);
        command.Parameters.Add("@CodeTourneeRetour", SqlDbType.NVarChar, 50).Value = ToDbValue(ligne.Retour?.CodeTourneeRetour);
        command.Parameters.Add("@LibelleTourneeRetour", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.Retour?.LibelleTourneeRetour);

        command.Parameters.Add("@Instructions", SqlDbType.NVarChar, 1000).Value = ToDbValue(ligne.InfosLivreur?.Instructions);
        command.Parameters.Add("@CommentaireExceptionnel", SqlDbType.NVarChar, 1000).Value = ToDbValue(ligne.InfosLivreur?.CommentaireExceptionnel);
        command.Parameters.Add("@ZoneDechargement", SqlDbType.NVarChar, 100).Value = ToDbValue(ligne.InfosLivreur?.ZoneDechargement);
        command.Parameters.Add("@ZoneDechargementAffichee", SqlDbType.NVarChar, 150).Value = ToDbValue(ligne.InfosLivreur?.ZoneDechargementAffichee);
        command.Parameters.Add("@Zone", SqlDbType.NVarChar, 100).Value = ToDbValue(ligne.InfosLivreur?.Zone);
        command.Parameters.Add("@PrecisionInfo", SqlDbType.NVarChar, 1000).Value = ToDbValue(ligne.InfosLivreur?.Precision);
        command.Parameters.Add("@Cle", SqlDbType.NVarChar, 100).Value = ToDbValue(ligne.InfosLivreur?.Cle);
        command.Parameters.Add("@EstFerme", SqlDbType.Bit).Value = ligne.InfosLivreur?.EstFerme ?? false;
        command.Parameters.Add("@DateFermeture", SqlDbType.Date).Value = ToDbValue(ligne.InfosLivreur?.DateFermeture);
        command.Parameters.Add("@MotifFermeture", SqlDbType.NVarChar, 255).Value = ToDbValue(ligne.InfosLivreur?.MotifFermeture);

        command.Parameters.Add("@QuantiteLivree", SqlDbType.Int).Value = totalLivre;
        command.Parameters.Add("@QuantiteReprise", SqlDbType.Int).Value = totalRecupere;
        command.Parameters.Add("@NbRolls", SqlDbType.Int).Value = nbRolls;
        command.Parameters.Add("@NbTapis", SqlDbType.Int).Value = nbTapis;
        command.Parameters.Add("@NbSacs", SqlDbType.Int).Value = nbSacs;
        command.Parameters.Add("@NbRecuperes", SqlDbType.Int).Value = totalRecupere;

        command.Parameters.Add("@PrecisionLivreur", SqlDbType.NVarChar, 1000).Value = ToDbValue(ligne.Saisie?.PrecisionLivreur);
        // Comportement existant : le statut FAIT reste la valeur de repli si la saisie ne fournit pas de statut.
        command.Parameters.Add("@StatutPassage", SqlDbType.NVarChar, 30).Value = ligne.Saisie?.StatutPassage?.Trim() ?? "FAIT";
        command.Parameters.Add("@CommentaireLivreur", SqlDbType.NVarChar, 1000).Value = ToDbValue(ligne.Saisie?.CommentaireLivreur);
        command.Parameters.Add("@HeureValidation", SqlDbType.DateTimeOffset).Value = ToDbValue(ParseDateTimeOffsetOrNull(ligne.Saisie?.HeureValidation));
        command.Parameters.Add("@EstValidee", SqlDbType.Bit).Value = ligne.Saisie?.EstValidee ?? false;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

/// <summary>
/// Garantit l'existence de l'article saisissable avant d'insérer une quantité.
/// </summary>
/// <remarks>
/// Le mobile peut envoyer des articles du contrat de saisie. Le repository maintient
/// le référentiel Mobile_ArticleSaisissable actif afin que les quantités restent rattachées
/// à un code article connu.
/// </remarks>
    private async Task EnsureArticleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string codeArticle,
        string? libelle,
        CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (
    SELECT 1
    FROM dbo.Mobile_ArticleSaisissable WITH (UPDLOCK, HOLDLOCK)
    WHERE CodeArticle = @CodeArticle
)
BEGIN
    INSERT INTO dbo.Mobile_ArticleSaisissable
        (CodeArticle, LibelleArticle, OrdreAffichage, EstActif, EstVisibleMobile)
    VALUES
        (@CodeArticle, @LibelleArticle, 999, 1, 1);
END
ELSE
BEGIN
    UPDATE dbo.Mobile_ArticleSaisissable
    SET
        LibelleArticle = @LibelleArticle,
        EstActif = 1,
        EstVisibleMobile = 1,
        DateModification = SYSDATETIMEOFFSET()
    WHERE CodeArticle = @CodeArticle
      AND (
            LibelleArticle IS NULL
            OR LibelleArticle <> @LibelleArticle
          );
END
";

        var normalizedCodeArticle = codeArticle.Trim();
        var normalizedLibelle = NormalizeLibelleArticle(normalizedCodeArticle, libelle);

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CodeArticle", SqlDbType.NVarChar, 50).Value = normalizedCodeArticle;
        command.Parameters.Add("@LibelleArticle", SqlDbType.NVarChar, 100).Value = normalizedLibelle;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

/// <summary>
/// Insère le détail des quantités livrées, prévues et récupérées pour une ligne de tournée.
/// </summary>
/// <remarks>
/// Les quantités détaillées sont la source de vérité pour les articles saisis.
/// Elles permettent de distinguer la livraison, la reprise et la quantité prévue issue de l'Expédition.
/// </remarks>
    private async Task InsertQuantiteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long idTourneeLigne,
        SynchronisationQuantiteRequest quantite,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.Mobile_TourneeLigneQuantite
    (
        IdTourneeLigne,
        CodeArticle,
        LibelleArticle,
        QuantiteLivreePrevue,
        QuantiteLivree,
        QuantiteRecuperee
    )
VALUES
    (
        @IdTourneeLigne,
        @CodeArticle,
        @LibelleArticle,
        @QuantiteLivreePrevue,
        @QuantiteLivree,
        @QuantiteRecuperee
    );
";

        var normalized = NormalizeQuantite(quantite);

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdTourneeLigne", SqlDbType.BigInt).Value = idTourneeLigne;
        command.Parameters.Add("@CodeArticle", SqlDbType.NVarChar, 50).Value = normalized.CodeArticle.Trim();
        command.Parameters.Add("@LibelleArticle", SqlDbType.NVarChar, 100).Value = ToDbValue(normalized.Libelle);
        command.Parameters.Add("@QuantiteLivreePrevue", SqlDbType.Int).Value = ToDbValue(normalized.QuantiteLivreePrevue);
        command.Parameters.Add("@QuantiteLivree", SqlDbType.Int).Value = normalized.QuantiteLivree;
        command.Parameters.Add("@QuantiteRecuperee", SqlDbType.Int).Value = normalized.QuantiteRecuperee;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

/// <summary>
/// Écrit un événement de diagnostic lié à une synchronisation mobile.
/// </summary>
/// <remarks>
/// Les logs conservent le contexte utile à l'exploitation : tournée, livreur,
/// identifiant de synchronisation, appareil, version applicative et adresse IP.
/// </remarks>
    private async Task InsertLogAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long? idTourneeMobile,
        int? idLivreur,
        Guid? idSynchronisation,
        string typeEvenement,
        string niveau,
        string message,
        string? detailTechnique,
        string? adresseIp,
        string? nomAppareil,
        string? versionApplication,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.Mobile_LogSynchronisation
    (
        IdTourneeMobile,
        IdLivreur,
        IdSynchronisation,
        TypeEvenement,
        Niveau,
        Message,
        DetailTechnique,
        AdresseIP,
        NomAppareil,
        VersionApplication
    )
VALUES
    (
        @IdTourneeMobile,
        @IdLivreur,
        @IdSynchronisation,
        @TypeEvenement,
        @Niveau,
        @Message,
        @DetailTechnique,
        @AdresseIP,
        @NomAppareil,
        @VersionApplication
    );
";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdTourneeMobile", SqlDbType.BigInt).Value = ToDbValue(idTourneeMobile);
        command.Parameters.Add("@IdLivreur", SqlDbType.Int).Value = ToDbValue(idLivreur);
        command.Parameters.Add("@IdSynchronisation", SqlDbType.UniqueIdentifier).Value = ToDbValue(idSynchronisation);
        command.Parameters.Add("@TypeEvenement", SqlDbType.NVarChar, 50).Value = typeEvenement;
        command.Parameters.Add("@Niveau", SqlDbType.NVarChar, 20).Value = niveau;
        command.Parameters.Add("@Message", SqlDbType.NVarChar, 1000).Value = message;
        command.Parameters.Add("@DetailTechnique", SqlDbType.NVarChar).Value = ToDbValue(detailTechnique);
        command.Parameters.Add("@AdresseIP", SqlDbType.NVarChar, 50).Value = ToDbValue(adresseIp);
        command.Parameters.Add("@NomAppareil", SqlDbType.NVarChar, 100).Value = ToDbValue(nomAppareil);
        command.Parameters.Add("@VersionApplication", SqlDbType.NVarChar, 50).Value = ToDbValue(versionApplication);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

// Normalise la liste des quantités avant calcul des totaux et insertion SQL.
    private static List<SynchronisationQuantiteRequest> NormalizeQuantites(
        IEnumerable<SynchronisationQuantiteRequest> quantites)
    {
        return quantites.Select(NormalizeQuantite).ToList();
    }

/// <summary>
/// Normalise une quantité article selon les conventions persistées en base.
/// </summary>
/// <remarks>
/// Les codes articles sont stockés en majuscules. Le libellé de ROLLS_VIDES est forcé
/// pour garder un vocabulaire métier stable entre Expédition, mobile et base SQL.
/// </remarks>
    private static SynchronisationQuantiteRequest NormalizeQuantite(SynchronisationQuantiteRequest quantite)
    {
        var codeArticle = string.IsNullOrWhiteSpace(quantite.CodeArticle)
            ? string.Empty
            : quantite.CodeArticle.Trim().ToUpperInvariant();

        return new SynchronisationQuantiteRequest
        {
            CodeArticle = codeArticle,
            Libelle = NormalizeLibelleArticle(codeArticle, quantite.Libelle),
            QuantiteLivreePrevue = quantite.QuantiteLivreePrevue,
            QuantiteLivree = quantite.QuantiteLivree,
            QuantiteRecuperee = quantite.QuantiteRecuperee
        };
    }

/// <summary>
/// Détermine le libellé article à persister après normalisation du code article.
/// </summary>
/// <remarks>
/// Règle métier : ROLLS_VIDES doit toujours apparaître comme "Chariots vides",
/// même si le mobile envoie un libellé absent ou différent.
/// </remarks>
    private static string NormalizeLibelleArticle(string codeArticle, string? libelle)
    {
        if (IsRollsVides(codeArticle))
        {
            return LibelleArticleRollsVides;
        }

        return string.IsNullOrWhiteSpace(libelle)
            ? codeArticle.Trim()
            : libelle.Trim();
    }

// Indique si le code article correspond au cas métier ROLLS_VIDES.
    private static bool IsRollsVides(string? codeArticle)
    {
        return string.Equals(codeArticle?.Trim(), CodeArticleRollsVides, StringComparison.OrdinalIgnoreCase);
    }

// Convertit l'identifiant de synchronisation déjà validé en Guid exploitable SQL
    private static Guid ParseGuid(object? value)
    {
        if (SynchronisationTourneeValidator.TryParseGuid(value, out var guid))
        {
            return guid;
        }

        throw new InvalidOperationException("L'identifiant de synchronisation est invalide.");
    }

// Convertit une date mobile optionnelle en DateTimeOffset sans déclencher d'écriture invalide.
    private static DateTimeOffset? ParseDateTimeOffsetOrNull(object? value)
    {
        return SynchronisationTourneeValidator.TryParseDateTimeOffsetNullable(value, out var dateTimeOffset)
            ? dateTimeOffset
            : null;
    }

// Calcule le total livré d'un article précis pour alimenter les colonnes de synthèse de ligne.
    private static int GetQuantiteLivreeArticle(
        IEnumerable<SynchronisationQuantiteRequest> quantites,
        string codeArticle)
    {
        return quantites
            .Where(q => string.Equals(q.CodeArticle, codeArticle, StringComparison.OrdinalIgnoreCase))
            .Sum(q => q.QuantiteLivree);
    }

/// <summary>
/// Convertit les valeurs optionnelles au format attendu par SQL Server.
/// </summary>
/// <remarks>
/// Les chaînes nulles, vides ou composées d'espaces deviennent DBNull.Value afin de stocker
/// une absence réelle de valeur plutôt qu'un texte vide.
/// </remarks>
    private static object ToDbValue(object? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? DBNull.Value : text.Trim();
        }

        return value;
    }

// Lit une chaîne SQL nullable depuis un SqlDataReader.
    private static string? ReadNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

// Lit une date SQL nullable depuis un SqlDataReader pour les diagnostics de synchronisation.
    private static DateTimeOffset? ReadNullableDateTimeOffset(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}

// Projection SQL utilisée lorsqu'une tournée a déjà été envoyée pour la même date et le même code tournée.
public sealed class TourneeDejaEnvoyeeDto
{
    public long IdTourneeMobile { get; set; }

    public DateTime DateTournee { get; set; }

    public string CodeTournee { get; set; } = string.Empty;

    public string? LibelleTournee { get; set; }

    public string CodeLivreur { get; set; } = string.Empty;

    public string? NomLivreur { get; set; }

    public DateTimeOffset? DateEnvoi { get; set; }

    public DateTimeOffset? DateReceptionApi { get; set; }
}

// Projection SQL utilisée lorsqu'un IdSynchronisation a déjà été reçu par l'API.
public sealed class SynchronisationDejaRecueDto
{
    public long IdTourneeMobile { get; set; }

    public Guid IdSynchronisation { get; set; }

    public DateTime DateTournee { get; set; }

    public string CodeTournee { get; set; } = string.Empty;

    public string? LibelleTournee { get; set; }

    public string? CodeLivreur { get; set; }

    public string? NomLivreur { get; set; }

    public DateTimeOffset? DateEnvoi { get; set; }

    public DateTimeOffset? DateReceptionApi { get; set; }
}

// Résultat minimal retourné après l'enregistrement transactionnel d'une synchronisation mobile.
public sealed class SynchronisationEnregistrementResult
{
    public long IdTourneeMobile { get; set; }

    public Guid IdSynchronisation { get; set; }

    public DateTimeOffset DateReceptionApi { get; set; }

    public int NombreLignesRecues { get; set; }

    public int NombreQuantitesRecues { get; set; }
}
