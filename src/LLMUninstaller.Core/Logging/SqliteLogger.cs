using System.Text.Json;
using LLMUninstaller.Core.Models;
using Microsoft.Data.Sqlite;

namespace LLMUninstaller.Core.Logging;

public sealed class SqliteLogger : IAppLogger, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteLogger(string? dbPath = null)
    {
        var path = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LLMUninstaller", "logs.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = $"Data Source={path}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS log_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                event_type TEXT NOT NULL,
                path TEXT NOT NULL,
                details TEXT,
                size_bytes INTEGER
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task LogFoundModelAsync(ModelInfo model)
    {
        var details = JsonSerializer.Serialize(new
        {
            model.Type,
            model.OwnerApplication,
            model.IsProtectedPath
        });

        await InsertAsync("found", model.FullPath, details, model.SizeBytes);
    }

    public async Task LogDeletedModelAsync(ModelInfo model, long freedBytes)
    {
        var details = JsonSerializer.Serialize(new
        {
            model.Type,
            model.OwnerApplication,
            FreedBytes = freedBytes
        });

        await InsertAsync("deleted", model.FullPath, details, freedBytes);
    }

    public async Task LogErrorAsync(string context, string message) =>
        await InsertAsync("error", context, message, null);

    public async Task<IReadOnlyList<LogEntry>> GetEntriesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, timestamp, event_type, path, details, size_bytes FROM log_entries ORDER BY id DESC";

            var entries = new List<LogEntry>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                entries.Add(new LogEntry
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    EventType = reader.GetString(2),
                    Path = reader.GetString(3),
                    Details = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SizeBytes = reader.IsDBNull(5) ? null : reader.GetInt64(5)
                });
            }

            return entries;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task InsertAsync(string eventType, string path, string? details, long? sizeBytes)
    {
        await _lock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO log_entries (timestamp, event_type, path, details, size_bytes)
                VALUES ($ts, $type, $path, $details, $size);
                """;
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$type", eventType);
            cmd.Parameters.AddWithValue("$path", path);
            cmd.Parameters.AddWithValue("$details", (object?)details ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$size", (object?)sizeBytes ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}
