using Dapper;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Repositories;

/// <summary>
/// Repository pour l'enregistrement, la vérification et la consultation des synchronisations.
/// Utilise les tables mobiles :
/// - Mobile_Livreur
/// - Mobile_Tournee
/// - Mobile_TourneeLigne
/// - Mobile_LogSynchronisation
/// </summary>
public class SynchronisationsRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SynchronisationsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Vérifie si une synchronisation existe déjà dans Mobile_Tournee.
    /// </summary>
    public async Task<bool> SynchronisationExistsAsync(string idSynchronisation)
    {
        if (!Guid.TryParse(idSynchronisation, out var guid))
            return false;

        using var connection = _connectionFactory.CreateMobileConnection();

        var exists = await connection.QueryFirstOrDefaultAsync<int?>(
            @"SELECT 1
              FROM Mobile_Tournee
              WHERE IdSynchronisation = @IdSynchronisation;",
            new { IdSynchronisation = guid });

        return exists.HasValue;
    }

    /// <summary>
    /// Consulte les synchronisations envoyées.
    /// Filtres possibles :
    /// - dateTournee
    /// - codeTournee
    /// - codeLivreur
    /// </summary>
    public async Task<IReadOnlyList<SynchronisationResumeDto>> GetSynchronisationsAsync(
        DateTime? dateTournee,
        string? codeTournee,
        string? codeLivreur)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var result = await connection.QueryAsync<SynchronisationResumeDto>(
            @"SELECT
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

                  SUM(CASE WHEN tl.StatutPassage = 'FAIT' THEN 1 ELSE 0 END) AS NombreFaits,
                  SUM(CASE WHEN tl.StatutPassage = 'NON_FAIT' THEN 1 ELSE 0 END) AS NombreNonFaits,
                  SUM(CASE WHEN tl.StatutPassage = 'ANOMALIE' THEN 1 ELSE 0 END) AS NombreAnomalies

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

              ORDER BY t.DateReceptionApi DESC, t.IdTourneeMobile DESC;",
            new
            {
                DateTournee = dateTournee?.Date,
                CodeTournee = string.IsNullOrWhiteSpace(codeTournee) ? null : codeTournee,
                CodeLivreur = string.IsNullOrWhiteSpace(codeLivreur) ? null : codeLivreur
            });

        return result.ToList();
    }

    /// <summary>
    /// Consulte le détail complet d'une synchronisation envoyée.
    /// Inclut :
    /// - l'entête de tournée
    /// - les lignes saisies
    /// - les logs associés
    /// </summary>
    public async Task<SynchronisationDetailDto?> GetSynchronisationByIdAsync(long idTourneeMobile)
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var detail = await connection.QuerySingleOrDefaultAsync<SynchronisationDetailDto>(
            @"SELECT
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
                        AND x.StatutPassage = 'FAIT'
                  ) AS NombreFaits,

                  (
                      SELECT COUNT(1)
                      FROM Mobile_TourneeLigne x
                      WHERE x.IdTourneeMobile = t.IdTourneeMobile
                        AND x.StatutPassage = 'NON_FAIT'
                  ) AS NombreNonFaits,

                  (
                      SELECT COUNT(1)
                      FROM Mobile_TourneeLigne x
                      WHERE x.IdTourneeMobile = t.IdTourneeMobile
                        AND x.StatutPassage = 'ANOMALIE'
                  ) AS NombreAnomalies

              FROM Mobile_Tournee t
              LEFT JOIN Mobile_Livreur l
                     ON l.IdLivreur = t.IdLivreur
              WHERE t.IdTourneeMobile = @IdTourneeMobile;",
            new { IdTourneeMobile = idTourneeMobile });

        if (detail is null)
            return null;

        var lignes = await connection.QueryAsync<SynchronisationLigneDto>(
            @"SELECT
                  IdTourneeLigne,
                  IdTourneeMobile,

                  OrdreArret,

                  NumClient,
                  NomClient,

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

              FROM Mobile_TourneeLigne
              WHERE IdTourneeMobile = @IdTourneeMobile
              ORDER BY OrdreArret, IdTourneeLigne;",
            new { IdTourneeMobile = idTourneeMobile });

        var logs = await connection.QueryAsync<SynchronisationLogDto>(
            @"SELECT
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
              ORDER BY DateEvenement DESC, IdLog DESC;",
            new { IdTourneeMobile = idTourneeMobile });

        detail.Lignes = lignes.ToList();
        detail.Logs = logs.ToList();

        return detail;
    }

    /// <summary>
    /// Enregistre une synchronisation de tournée complète.
    /// </summary>
    public async Task SaveSynchronisationAsync(SynchronisationTourneeRequest request)
    {
        using var connection = _connectionFactory.CreateMobileConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var now = DateTime.Now;

            if (!Guid.TryParse(request.IdSynchronisation, out var idSynchronisationGuid))
                throw new ArgumentException("IdSynchronisation doit être un GUID valide.");

            if (!DateTime.TryParse(request.DateTournee, out var dateTournee))
                throw new ArgumentException("DateTournee invalide.");

            DateTime? dateChargementMobile = null;
            if (!string.IsNullOrWhiteSpace(request.Mobile?.DateChargementMobile) &&
                DateTime.TryParse(request.Mobile.DateChargementMobile, out var parsedChargement))
            {
                dateChargementMobile = parsedChargement;
            }

            DateTime? dateEnvoiMobile = null;
            if (!string.IsNullOrWhiteSpace(request.Mobile?.DateEnvoiMobile) &&
                DateTime.TryParse(request.Mobile.DateEnvoiMobile, out var parsedEnvoi))
            {
                dateEnvoiMobile = parsedEnvoi;
            }

            await connection.ExecuteAsync(
                @"MERGE INTO Mobile_Livreur AS target
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
                      INSERT
                      (
                          CodeLivreur,
                          NomLivreur,
                          EstActif,
                          DateCreation,
                          DateModification
                      )
                      VALUES
                      (
                          source.CodeLivreur,
                          source.NomLivreur,
                          1,
                          @Now,
                          NULL
                      );",
                new
                {
                    CodeLivreur = request.Livreur?.CodeLivreur,
                    NomLivreur = request.Livreur?.NomLivreur,
                    Now = now
                },
                transaction);

            var idLivreur = await connection.QuerySingleAsync<int>(
                @"SELECT IdLivreur
                  FROM Mobile_Livreur
                  WHERE CodeLivreur = @CodeLivreur;",
                new { CodeLivreur = request.Livreur?.CodeLivreur },
                transaction);

            var idTourneeMobile = await connection.QuerySingleAsync<long>(
                @"INSERT INTO Mobile_Tournee
                  (
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
                  VALUES
                  (
                      @IdSynchronisation,
                      @DateTournee,
                      @CodeTournee,
                      @LibelleTournee,
                      @IdLivreur,
                      'ENVOYEE',
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
                  );",
                new
                {
                    IdSynchronisation = idSynchronisationGuid,
                    DateTournee = dateTournee.Date,
                    CodeTournee = request.CodeTournee,
                    LibelleTournee = request.LibelleTournee,
                    IdLivreur = idLivreur,
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
                var saisie = ligne.Saisie;

                var nbExpes = saisie?.NbExpes ?? 0;
                var nbRolls = saisie?.NbRolls ?? 0;
                var nbVetements = saisie?.NbVetements ?? 0;
                var nbTapis = saisie?.NbTapis ?? 0;
                var nbSacs = saisie?.NbSacs ?? 0;
                var nbRecuperes = saisie?.NbRecuperes ?? 0;

                var quantiteLivree =
                    nbExpes +
                    nbRolls +
                    nbVetements +
                    nbTapis +
                    nbSacs;

                var quantiteReprise = nbRecuperes;

                DateTime? heureValidation = null;
                if (!string.IsNullOrWhiteSpace(saisie?.HeureValidation))
                {
                    if (TimeSpan.TryParse(saisie.HeureValidation, out var heureSimple))
                    {
                        heureValidation = dateTournee.Date.Add(heureSimple);
                    }
                    else if (DateTime.TryParse(saisie.HeureValidation, out var heureComplete))
                    {
                        heureValidation = heureComplete;
                    }
                }

                await connection.ExecuteAsync(
                    @"INSERT INTO Mobile_TourneeLigne
                      (
                          IdTourneeMobile,
                          CodeTournee,
                          OrdreArret,
                          NumClient,
                          NomClient,
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
                      VALUES
                      (
                          @IdTourneeMobile,
                          @CodeTournee,
                          @OrdreArret,
                          @NumClient,
                          @NomClient,
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
                      );",
                    new
                    {
                        IdTourneeMobile = idTourneeMobile,
                        CodeTournee = request.CodeTournee,
                        OrdreArret = ligne.OrdreArret,
                        NumClient = ligne.Client?.NumClient,
                        NomClient = ligne.Client?.NomClient,
                        CodePDL = ligne.PointLivraison?.CodePDL,
                        DescriptionPDL = ligne.PointLivraison?.DescriptionPDL,
                        NbExpes = nbExpes,
                        NbRolls = nbRolls,
                        NbVetements = nbVetements,
                        NbTapis = nbTapis,
                        NbSacs = nbSacs,
                        NbRecuperes = nbRecuperes,
                        QuantiteLivree = quantiteLivree,
                        QuantiteReprise = quantiteReprise,
                        PrecisionLivreur = saisie?.PrecisionLivreur,
                        StatutPassage = saisie?.StatutPassage,
                        CommentaireLivreur = saisie?.CommentaireLivreur,
                        HeureValidation = heureValidation,
                        EstValidee = saisie?.EstValidee ?? false,
                        Now = now
                    },
                    transaction);
            }

            await connection.ExecuteAsync(
                @"INSERT INTO Mobile_LogSynchronisation
                  (
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
                  VALUES
                  (
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
                  );",
                new
                {
                    IdTourneeMobile = idTourneeMobile,
                    IdLivreur = idLivreur,
                    IdSynchronisation = idSynchronisationGuid,
                    DateEvenement = now,
                    TypeEvenement = "ENVOI_REUSSI",
                    Niveau = "INFO",
                    Message = "Synchronisation enregistrée avec succès.",
                    DetailTechnique = $"Tournée {request.CodeTournee} du {dateTournee:yyyy-MM-dd} synchronisée avec {request.Lignes.Count} ligne(s).",
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
}