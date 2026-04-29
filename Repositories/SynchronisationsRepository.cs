using Dapper;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;
using System.Data;

namespace API_ASP.NET_Core.Repositories;

public class SynchronisationsRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SynchronisationsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Vérifie si une synchronisation avec le même IdSynchronisation existe déjà
    /// </summary>
    public async Task<bool> SynchronisationExistsAsync(string idSynchronisation)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var exists = await connection.QueryFirstOrDefaultAsync(
            "SELECT 1 FROM Mobile_Synchronisation WHERE IdSynchronisation = @IdSync",
            new { IdSync = idSynchronisation });

        return exists != null;
    }

    /// <summary>
    /// Enregistre une synchronisation complète dans la base Mobile_
    /// </summary>
    public async Task SaveSynchronisationAsync(SynchronisationTourneeRequest request)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Insérer la synchronisation
            await connection.ExecuteAsync(
                @"INSERT INTO Mobile_Synchronisation 
                  (IdSynchronisation, DateSynchronisation, SchemaVersion, CodeTournee, DateTournee, CommentaireGlobal)
                  VALUES (@IdSync, @DateSync, @SchemaVersion, @CodeTournee, @DateTournee, @CommentaireGlobal)",
                new
                {
                    IdSync = request.IdSynchronisation,
                    DateSync = DateTime.Now,
                    SchemaVersion = request.SchemaVersion,
                    CodeTournee = request.CodeTournee,
                    DateTournee = request.DateTournee,
                    CommentaireGlobal = request.CommentaireGlobal
                },
                transaction);

            // 2. Insérer ou mettre à jour le livreur
            await connection.ExecuteAsync(
                @"MERGE INTO Mobile_Livreur AS target
                  USING (SELECT @CodeLivreur AS CodeLivreur) AS source
                  ON target.CodeLivreur = source.CodeLivreur
                  WHEN MATCHED THEN
                    UPDATE SET NomLivreur = @NomLivreur, DateMajLivreur = @DateMaj
                  WHEN NOT MATCHED THEN
                    INSERT (CodeLivreur, NomLivreur, DateMajLivreur) 
                    VALUES (@CodeLivreur, @NomLivreur, @DateMaj)",
                new
                {
                    CodeLivreur = request.Livreur?.CodeLivreur,
                    NomLivreur = request.Livreur?.NomLivreur,
                    DateMaj = DateTime.Now
                },
                transaction);

            // 3. Boucle sur les lignes et insertion
            foreach (var ligne in request.Lignes)
            {
                var saisie = ligne.Saisie;
                var quantiteLivree = (saisie?.NbExpes ?? 0) + (saisie?.NbRolls ?? 0) + 
                                     (saisie?.NbVetements ?? 0) + (saisie?.NbTapis ?? 0) + 
                                     (saisie?.NbSacs ?? 0);
                var quantiteReprise = saisie?.NbRecuperes ?? 0;

                // Convertir heureValidation string en DateTime
                DateTime? heureValidation = null;
                if (!string.IsNullOrWhiteSpace(saisie?.HeureValidation))
                {
                    if (DateTime.TryParse(saisie.HeureValidation, out var parsedHeure))
                        heureValidation = parsedHeure;
                }

                await connection.ExecuteAsync(
                    @"INSERT INTO Mobile_SynchronisationLigne 
                      (IdSynchronisation, IdLigneSource, OrdreArret, NumClient, NomClient, NomAffichage,
                       CodePDL, DescriptionPDL, NbExpes, NbRolls, NbVetements, NbTapis, NbSacs, NbRecuperes,
                       QuantiteLivree, QuantiteReprise, PrecisionLivreur, StatutPassage, CommentaireLivreur, 
                       HeureValidation, EstValidee, DateEnregistrement)
                      VALUES (@IdSync, @IdLigneSource, @OrdreArret, @NumClient, @NomClient, @NomAffiche,
                              @CodePDL, @DescriptionPDL, @NbExpes, @NbRolls, @NbVetements, @NbTapis, @NbSacs, 
                              @NbRecuperes, @QuantiteLivree, @QuantiteReprise, @PrecisionLivreur, @StatutPassage, 
                              @CommentaireLivreur, @HeureValidation, @EstValidee, @DateEnregistrement)",
                    new
                    {
                        IdSync = request.IdSynchronisation,
                        IdLigneSource = ligne.IdLigneSource,
                        OrdreArret = ligne.OrdreArret,
                        NumClient = ligne.Client?.NumClient,
                        NomClient = ligne.Client?.NomClient,
                        NomAffiche = ligne.Client?.NomAffiche,
                        CodePDL = ligne.PointLivraison?.CodePDL,
                        DescriptionPDL = ligne.PointLivraison?.DescriptionPDL,
                        NbExpes = saisie?.NbExpes,
                        NbRolls = saisie?.NbRolls,
                        NbVetements = saisie?.NbVetements,
                        NbTapis = saisie?.NbTapis,
                        NbSacs = saisie?.NbSacs,
                        NbRecuperes = saisie?.NbRecuperes,
                        QuantiteLivree = quantiteLivree,
                        QuantiteReprise = quantiteReprise,
                        PrecisionLivreur = saisie?.PrecisionLivreur,
                        StatutPassage = saisie?.StatutPassage,
                        CommentaireLivreur = saisie?.CommentaireLivreur,
                        HeureValidation = heureValidation,
                        EstValidee = saisie?.EstValidee,
                        DateEnregistrement = DateTime.Now
                    },
                    transaction);
            }

            // 4. Enregistrer le log
            await connection.ExecuteAsync(
                @"INSERT INTO Mobile_LogSynchronisation (IdSynchronisation, Type, DateLog)
                  VALUES (@IdSync, @Type, @DateLog)",
                new
                {
                    IdSync = request.IdSynchronisation,
                    Type = "ENVOI_REUSSI",
                    DateLog = DateTime.Now
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
