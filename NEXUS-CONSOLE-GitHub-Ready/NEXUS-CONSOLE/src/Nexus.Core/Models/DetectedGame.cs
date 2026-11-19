namespace Nexus.Core.Models;

/// <summary>Jeu trouvé par une source de détection, avant rapprochement avec la base.</summary>
public sealed record DetectedGame(
    string Platform, string ExternalId, string Name,
    string? InstallPath, string? ExePath, string? LaunchUri, string? CoverPath,
    long? PlaytimeSeconds = null, DateTime? LastPlayedUtc = null);
