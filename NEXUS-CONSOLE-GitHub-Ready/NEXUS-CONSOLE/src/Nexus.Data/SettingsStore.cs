using Nexus.Core.Config;

namespace Nexus.Data;

public sealed class SettingsStore
{
    private const int DefaultProfileId = 1;
    private readonly Database _db;

    public SettingsStore(Database db) => _db = db;

    public AppSettings Load(int profileId = DefaultProfileId)
    {
        var s = new AppSettings();
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings WHERE profile_id = $p";
        cmd.Parameters.AddWithValue("$p", profileId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) s.Apply(r.GetString(0), r.GetString(1));
        return s;
    }

    public void Save(AppSettings s, int profileId = DefaultProfileId)
    {
        using var c = _db.Open();
        using var tx = c.BeginTransaction();
        foreach (var kv in s.ToPairs())
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO settings (profile_id, key, value) VALUES ($p, $k, $v) " +
                              "ON CONFLICT(profile_id, key) DO UPDATE SET value = excluded.value";
            cmd.Parameters.AddWithValue("$p", profileId);
            cmd.Parameters.AddWithValue("$k", kv.Key);
            cmd.Parameters.AddWithValue("$v", kv.Value);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
