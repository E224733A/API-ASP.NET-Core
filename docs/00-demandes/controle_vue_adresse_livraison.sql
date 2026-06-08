/* ============================================================
   CONTROLE VUE ADRESSE LIVRAISON MOBILESLI

   Vue attendue : [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
   Colonnes     : CodePDL, AdresseLivraison

   Lecture seule.
   ============================================================ */

SET NOCOUNT ON;

IF OBJECT_ID(N'lavinprosli.dbo.v_Mobile_AdresseLivraison', N'V') IS NULL
BEGIN
    THROW 51000, N'Vue [lavinprosli].[dbo].[v_Mobile_AdresseLivraison] introuvable.', 1;
END;

SELECT TOP (50)
    CodePDL,
    AdresseLivraison,
    CASE
        WHEN CodePDL IS NULL OR LTRIM(RTRIM(CAST(CodePDL AS nvarchar(100)))) = N'' THEN N'ERREUR_CODEPDL_VIDE'
        WHEN AdresseLivraison IS NULL OR LTRIM(RTRIM(CAST(AdresseLivraison AS nvarchar(2048)))) = N'' THEN N'ERREUR_ADRESSELIVRAISON_VIDE'
        WHEN TRY_CONVERT(nvarchar(2048), AdresseLivraison) NOT LIKE N'http%' THEN N'ATTENTION_URL_NON_HTTP'
        ELSE N'OK'
    END AS ControleVue
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
ORDER BY CodePDL;

SELECT
    CodePDL,
    COUNT(*) AS NombreOccurrences
FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
GROUP BY CodePDL
HAVING COUNT(*) > 1
ORDER BY NombreOccurrences DESC, CodePDL;
