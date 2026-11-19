namespace Nexus.Data;

/// <summary>Migrations numérotées (PRAGMA user_version). Ne jamais modifier une étape existante : en ajouter une.</summary>
internal static class Migrations
{
    public static readonly string[] Steps =
    {
        """
        CREATE TABLE profiles (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            avatar TEXT,
            created_utc TEXT NOT NULL
        );
        INSERT INTO profiles (id, name, created_utc) VALUES (1, 'Joueur', datetime('now'));
        CREATE TABLE settings (
            profile_id INTEGER NOT NULL REFERENCES profiles(id),
            key TEXT NOT NULL,
            value TEXT NOT NULL,
            PRIMARY KEY (profile_id, key)
        );
        """,
        """
        CREATE TABLE games (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            platform TEXT NOT NULL,
            external_id TEXT NOT NULL,
            exe_path TEXT,
            install_path TEXT,
            icon_path TEXT,
            cover_path TEXT,
            favorite INTEGER NOT NULL DEFAULT 0,
            hidden INTEGER NOT NULL DEFAULT 0,
            last_played_utc TEXT,
            playtime_s INTEGER NOT NULL DEFAULT 0,
            added_utc TEXT NOT NULL,
            UNIQUE (platform, external_id)
        );
        """,
        """
        ALTER TABLE games ADD COLUMN installed INTEGER NOT NULL DEFAULT 1;
        ALTER TABLE games ADD COLUMN launch_uri TEXT;
        """,
        """
        CREATE TABLE app_state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """
    };
}
