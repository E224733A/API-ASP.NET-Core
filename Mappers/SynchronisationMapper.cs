using System;

namespace API_ASP.NET_Core.Mappers;

/// <summary>
/// Mapper dédié au module de synchronisation mobile.
/// Cette classe contient des méthodes de conversion simples
/// qui ne relèvent ni du contrôle HTTP ni de l'accès SQL.
/// L'objectif principal est de centraliser les transformations
/// élémentaires pour alléger la logique dans le contrôleur
/// et le service.
/// </summary>
public sealed class SynchronisationMapper
{
    /// <summary>
    /// Formate une valeur potentiellement DateTime en chaîne au format yyyy-MM-dd.
    /// </summary>
    /// <param name="value">Valeur à formater (DateTime, DateOnly, string ou null).</param>
    /// <returns>Une chaîne formatée yyyy-MM-dd ou la représentation brute si la conversion échoue.</returns>
    public string FormatDateTournee(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        // Essayez de convertir à partir d'une chaîne ou d'un autre objet convertible.
        if (DateTime.TryParse(Convert.ToString(value), out var parsedDate))
        {
            return parsedDate.ToString("yyyy-MM-dd");
        }

        // Si la conversion échoue, renvoyez la valeur brute ou une chaîne vide.
        return Convert.ToString(value) ?? string.Empty;
    }
}