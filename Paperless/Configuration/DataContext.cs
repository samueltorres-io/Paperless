using Microsoft.Data.Sqlite;

namespace Paperless.Configuration;

public abstract class DataContext
{
    protected readonly string _connectionString;

    public DataContext(string databasePath = "./data/database.db")
    {
        _connectionString = $"Data Source={databasePath}";
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
}