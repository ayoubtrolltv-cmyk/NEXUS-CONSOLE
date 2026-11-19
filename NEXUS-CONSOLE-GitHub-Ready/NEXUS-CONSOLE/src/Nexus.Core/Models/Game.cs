namespace Nexus.Core.Models;

public sealed class Game
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "manual";
    public string ExternalId { get; set; } = "";
    public string? ExePath { get; set; }
    public string? InstallPath { get; set; }
    public string? IconPath { get; set; }
    public string? CoverPath { get; set; }
    public bool Favorite { get; set; }
    public bool Hidden { get; set; }
    public bool Installed { get; set; } = true;
    public string? LaunchUri { get; set; }

    /// <summary>Arguments de la ligne de commande (non enregistré en base : sert aux entrées temporaires, ex. services cloud).</summary>
    public string? Arguments { get; set; }

    /// <summary>Si vrai, la session se termine quand le processus lancé se ferme (non enregistré en base).</summary>
    public bool TrackLaunchedProcess { get; set; }
    public DateTime? LastPlayedUtc { get; set; }
    public long PlaytimeSeconds { get; set; }
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}
