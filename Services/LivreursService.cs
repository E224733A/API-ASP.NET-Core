using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service d'accès aux livreurs.
///
/// Ce service encapsule l'appel au repository SQL afin de respecter
/// l'architecture MVC et de permettre à la couche contrôleur de rester légère.
/// Aucune logique métier supplémentaire n'est ajoutée ici : le repository reste
/// responsable de l'accès aux données.
/// </summary>
public sealed class LivreursService
{
    private readonly LivreursRepository _livreursRepository;

    public LivreursService(LivreursRepository livreursRepository)
    {
        _livreursRepository = livreursRepository;
    }

    /// <summary>
    /// Retourne la liste des livreurs/chauffeurs disponibles.
    /// </summary>
    /// <remarks>
    /// Cette méthode délègue directement au repository afin de ne pas
    /// répéter la logique de récupération des données SQL dans le
    /// contrôleur. Elle permet également d'injecter d'autres services
    /// éventuels (ex. cache) sans modifier le contrôleur.
    /// </remarks>
    /// <returns>Liste des livreurs.</returns>
    public Task<IEnumerable<LivreurDto>> GetAllAsync()
    {
        return _livreursRepository.GetAllAsync();
    }
}