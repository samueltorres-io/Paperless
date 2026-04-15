using Microsoft.Data.Sqlite;
using System.IO;

namespace Paperless.Configuration;

public abstract class DataContext
{
    protected readonly string _connectionString;

    public DataContext(string databasePath = "./data/database.db")
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));
        }

        if (!IsInMemory(databasePath))
        {
            EnsureDatabaseDirectoryExists(databasePath);
        }

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        };

        _connectionString = csb.ToString();
        Initialize();
    }

    protected SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private void Initialize()
    {
        using var conn = GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = GetSchema();

        cmd.ExecuteNonQuery();
    }

    protected abstract string GetSchema();

    private static bool IsInMemory(string databasePath) =>
        databasePath.Trim().Equals(":memory:", StringComparison.OrdinalIgnoreCase);

    private static void EnsureDatabaseDirectoryExists(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }
}