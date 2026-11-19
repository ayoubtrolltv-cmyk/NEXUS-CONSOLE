using System.Globalization;
using Nexus.Core.Models;

namespace Nexus.Data;

/// <summary>Profils locaux et profil actif. Chaque profil a ses propres paramètres (thème, sons, manette…).</summary>
public sealed class ProfileRepository
{
    private readonly Database _db;

    public ProfileRepository(Database db) => _db = db;

    public IReadOnlyList<Profile> List()
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_utc FROM profiles ORDER BY id";
        using var r = cmd.ExecuteReader();
        var list = new List<Profile>();
        while (r.Read())
            list.Add(new Profile(r.GetInt32(0), r.GetString(1),
                DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)));
        return list;
    }

    public Profile? Get(int id) => List().FirstOrDefault(p => p.Id == id);

    public Profile Add(string name)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO profiles (name, created_utc) VALUES ($n, $c)";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$c", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
        using var idCmd = c.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid()";
        var id = Convert.ToInt32(idCmd.ExecuteScalar());
        return new Profile(id, name, DateTime.UtcNow);
    }

    /// <summary>Supprime un profil et ses paramètres. Refuse de supprimer le dernier profil.</summary>
    public bool Delete(int id)
    {
        if (List().Count <= 1) return false;
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        foreach (var sql in new[] { "DELETE FROM settings WHERE profile_id = $id", "DELETE FROM profiles WHERE id = $id" })
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }

    /// <summary>Profil actif mémorisé ; à défaut (ou s'il n'existe plus) le premier profil.</summary>
    public int GetCurrentId()
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_state WHERE key = 'current_profile'";
        var stored = cmd.ExecuteScalar() as string;
        var profiles = List();
        if (int.TryParse(stored, out var id) && profiles.Any(p => p.Id == id)) return id;
        return profiles.Count > 0 ? profiles[0].Id : 1;
    }

    public void SetCurrentId(int id)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO app_state (key, value) VALUES ('current_profile', $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$v", id.ToString(CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }
}
