using Microsoft.Data.Sqlite;
using Paperless.Configuration;

namespace Paperless.Tests.Configuration;

public class TestDataContext : DataContext
{
    public TestDataContext(string databasePath) : base(databasePath) { }

    protected override string GetSchema() => @"
        CREATE TABLE IF NOT EXISTS test_table (
            id   TEXT PRIMARY KEY,
            name TEXT NOT NULL
        );
    ";

    public SqliteConnection CreateConnection() => GetConnection();
}

public class DataContextTests : IDisposable
{
    private readonly string _dbPath;

    public DataContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dc_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); }
        catch { /* cleanup */ }
    }

    [Fact]
    public void Constructor_ShouldCreateDatabaseFile()
    {
        _ = new TestDataContext(_dbPath);

        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void Constructor_ShouldExecuteSchema()
    {
        var ctx = new TestDataContext(_dbPath);

        using var conn = ctx.CreateConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO test_table (id, name) VALUES ('1', 'Teste')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT name FROM test_table WHERE id = '1'";
        var result = cmd.ExecuteScalar();

        Assert.Equal("Teste", result);
    }

    [Fact]
    public void Constructor_CalledTwice_ShouldNotThrow()
    {
        _ = new TestDataContext(_dbPath);
        _ = new TestDataContext(_dbPath);
    }

    [Fact]
    public void GetConnection_ShouldReturnWorkingConnection()
    {
        var ctx = new TestDataContext(_dbPath);

        using var conn = ctx.CreateConnection();
        conn.Open();

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }
}
