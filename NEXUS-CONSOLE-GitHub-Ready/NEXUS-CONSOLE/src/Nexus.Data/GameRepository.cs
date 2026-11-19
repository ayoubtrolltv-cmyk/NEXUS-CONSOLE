using System.Globalization;
using Microsoft.Data.Sqlite;
using Nexus.Core.Models;

namespace Nexus.Data;

public sealed class GameRepository
{
    private const string Cols =
        "id, name, platform, external_id, exe_path, install_path, icon_path, cover_path, favorite, hidden, last_played_utc, playtime_s, added_utc, installed, launch_uri";

    private readonly Database _db;

    public GameRepository(Database db) => _db = db;

    public IReadOnlyList<Game> List(bool includeHidden = false)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM games" + (includeHidden ? "" : " WHERE hidden = 0");
        using var r = cmd.ExecuteReader();
        var list = new List<Game>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    public Game? Get(long id)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM games WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>Ajoute un jeu. Retourne null si (plateforme, identifiant externe) existe déjà.</summary>
    public Game? Add(Game g)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT OR IGNORE INTO games (name, platform, external_id, exe_path, install_path, icon_path, cover_path, favorite, hidden, last_played_utc, playtime_s, added_utc, installed, launch_uri) " +
            "VALUES ($n, $p, $e, $x, $i, $ic, $cv, $f, $h, $lp, $pt, $a, $inst, $lu)";
        cmd.Parameters.AddWithValue("$n", g.Name);
        cmd.Parameters.AddWithValue("$p", g.Platform);
        cmd.Parameters.AddWithValue("$e", g.ExternalId);
        cmd.Parameters.AddWithValue("$x", (object?)g.ExePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$i", (object?)g.InstallPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ic", (object?)g.IconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cv", (object?)g.CoverPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", g.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$h", g.Hidden ? 1 : 0);
        cmd.Parameters.AddWithValue("$lp", g.LastPlayedUtc is { } d ? d.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("$pt", g.PlaytimeSeconds);
        cmd.Parameters.AddWithValue("$a", g.AddedUtc.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$inst", g.Installed ? 1 : 0);
        cmd.Parameters.AddWithValue("$lu", (object?)g.LaunchUri ?? DBNull.Value);
        if (cmd.ExecuteNonQuery() == 0) return null;

        using var idCmd = c.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        g.Id = Convert.ToInt64(idCmd.ExecuteScalar());
        return g;
    }


    public Game? Find(string platform, string externalId)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Cols} FROM games WHERE platform = $p AND external_id = $e";
        cmd.Parameters.AddWithValue("$p", platform);
        cmd.Parameters.AddWithValue("$e", externalId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>Met à jour un jeu détecté sans toucher aux choix de l'utilisateur (favori, masqué).</summary>
    public void UpdateDetected(long id, Game g)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "UPDATE games SET name = $n, exe_path = $x, install_path = $i, " +
            "icon_path = COALESCE($ic, icon_path), cover_path = COALESCE($cv, cover_path), launch_uri = $lu, installed = 1, " +
            "playtime_s = MAX(playtime_s, $pt), " +
            "last_played_utc = CASE WHEN $lp IS NOT NULL AND (last_played_utc IS NULL OR $lp > last_played_utc) THEN $lp ELSE last_played_utc END " +
            "WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", g.Name);
        cmd.Parameters.AddWithValue("$x", (object?)g.ExePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$i", (object?)g.InstallPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ic", (object?)g.IconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cv", (object?)g.CoverPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lu", (object?)g.LaunchUri ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pt", g.PlaytimeSeconds);
        cmd.Parameters.AddWithValue("$lp", g.LastPlayedUtc is { } d ? d.ToString("o", CultureInfo.InvariantCulture) : DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetInstalled(long id, bool value) => SetFlag(id, "installed", value);

    /// <summary>Enregistre une session de jeu : date de dernière utilisation et temps cumulé.</summary>
    public void RecordSession(long id, DateTime startedUtc, long seconds)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE games SET last_played_utc = $lp, playtime_s = playtime_s + $s WHERE id = $id";
        cmd.Parameters.AddWithValue("$lp", startedUtc.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$s", Math.Max(0, seconds));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetFavorite(long id, bool value) => SetFlag(id, "favorite", value);
    public void SetHidden(long id, bool value) => SetFlag(id, "hidden", value);

    /// <summary>Retire le jeu de NEXUS. Ne touche jamais aux fichiers du jeu.</summary>
    public void Remove(long id)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM games WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private void SetFlag(long id, string column, bool value)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"UPDATE games SET {column} = $v WHERE id = $id";   // colonne interne, jamais saisie par l'utilisateur
        cmd.Parameters.AddWithValue("$v", value ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static string? Str(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static Game Read(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        Platform = r.GetString(2),
        ExternalId = r.GetString(3),
        ExePath = Str(r, 4),
        InstallPath = Str(r, 5),
        IconPath = Str(r, 6),
        CoverPath = Str(r, 7),
        Favorite = r.GetInt64(8) != 0,
        Hidden = r.GetInt64(9) != 0,
        LastPlayedUtc = Str(r, 10) is { } lp ? DateTime.Parse(lp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null,
        PlaytimeSeconds = r.GetInt64(11),
        AddedUtc = DateTime.Parse(r.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        Installed = r.GetInt64(13) != 0,
        LaunchUri = Str(r, 14)
    };
}
