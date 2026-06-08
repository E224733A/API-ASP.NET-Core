/* ============================================================
   CONTROLE VUE ADRESSE LIVRAISON MOBILESLI

   Vue attendue : [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
   Colonnes     : NUM_CLI, CodePDL, AdresseLivraison

   Regle metier : le lien d'adresse livraison est identifie par le couple
   NUM_CLI + CodePDL, jamais par CodePDL seul.

   Lecture seule.
   ============================================================ */

SET NOCOUNT ON;

IF OBJECT_ID(N'lavinprosli.dbo.v_Mobile_AdresseLivraison', N'V') IS NULL
BEGIN
    THROW 51000, N'Vue [lavinprosli].[dbo].[v_Mobile_AdresseLivraison] introuvable.', 1;
END;

PRINT N'--- 1. Controle des donnees exposees par la vue ---';

SELECT TOP (50)
    NUM_CLI,
    CodePDL,
    AdresseLivraison,
    CASE
        WHEN NUM_CLI IS NULL OR LTRIM(RTRIM(CAST(NUM_CLI AS nvarchar(50)))) = N'' THEN N'ERREUR_NUM_CLI_VIDE'
        WHEN CodePDL IS NULL OR LTRIM(RTRIM(CAST(CodePDL AS nvarchar(100)))) = N'' THEN N'ERREUR_CODEPDL_VIDE'
        WHEN AdresseLivraison IS NULL OR LTRIM(RTRIM(CAST(AdresseLivraison AS nvarchar(2048)))) = N'' THEN N'ERREUR_ADRESSELIVRAISON_VIDE'
        WHEN TRY_CONVERT(nvarchar(2048), AdresseLivraison) NOT LIKE N'http%' THEN N'ATTENTION_URL_NON_HTTP'
        ELSE N'OK'
    END AS ControleVue
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
ORDER BY
    NUM_CLI,
    CodePDL;

PRINT N'';
PRINT N'--- 2. Controle doublons interdits NUM_CLI + CodePDL ---';

SELECT
    NUM_CLI,
    CodePDL,
    COUNT(*) AS NombreOccurrences
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
GROUP BY
    NUM_CLI,
    CodePDL
HAVING COUNT(*) > 1
ORDER BY
    NombreOccurrences DESC,
    NUM_CLI,
    CodePDL;

PRINT N'';
PRINT N'--- 3. PDL ambigus differencies par client ---';

SELECT
    CodePDL,
    COUNT(DISTINCT NUM_CLI) AS NombreClients,
    COUNT(*) AS NombreLignes
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
GROUP BY CodePDL
HAVING COUNT(DISTINCT NUM_CLI) > 1
ORDER BY CodePDL;
