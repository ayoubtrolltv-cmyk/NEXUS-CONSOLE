using Nexus.Core.Models;

namespace Nexus.Detection;

public interface IGameSource
{
    /// <summary>Identifiant de plateforme stocké en base (steam, epic, gog, ubisoft…).</summary>
    string Platform { get; }

    /// <summary>Retourne les jeux installés. Liste vide si la plateforme est absente ; lève une exception seulement en cas d'erreur réelle.</summary>
    IReadOnlyList<DetectedGame> Scan();
}
