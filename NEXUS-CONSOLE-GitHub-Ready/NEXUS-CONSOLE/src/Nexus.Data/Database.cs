using Microsoft.Data.Sqlite;
using Nexus.Core.Logging;

namespace Nexus.Data;

public sealed class Database
{
    private readonly string _path;
    private readonly string _connectionString;

    public Database(string path)
    {
        _path = path;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    /// <summary>Crée ou migre la base. Un fichier corrompu est mis de côté (.corrupt-date) puis recréé.</summary>
    public void Initialize()
    {
        try { Migrate(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 11 or 26)   // SQLITE_CORRUPT / SQLITE_NOTADB
        {
            Log.Error("Base de données corrompue : elle est mise de côté et recréée.", ex);
            SqliteConnection.ClearAllPools();
            File.Move(_path, $"{_path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}");
            Migrate();
        }
    }

    private void Migrate()
    {
        using var c = Open();
        int current;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version;";
            current = Convert.ToInt32(cmd.ExecuteScalar());
        }
        if (current >= Migrations.Steps.Length) return;
        if (current > 0) File.Copy(_path, _path + ".bak", overwrite: true);   // sauvegarde avant migration

        for (var i = current; i < Migrations.Steps.Length; i++)
        {
            using var tx = c.BeginTransaction();
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = Migrations.Steps[i] + $"\nPRAGMA user_version={i + 1};";
            cmd.ExecuteNonQuery();
            tx.Commit();
            Log.Info($"Migration {i + 1} appliquée.");
        }
    }
}
