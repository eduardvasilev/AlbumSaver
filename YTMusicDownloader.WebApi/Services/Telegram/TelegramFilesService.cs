﻿using System.Threading;
using System.Threading.Tasks;

using Npgsql;

namespace YTMusicDownloader.WebApi.Services.Telegram;

public class TelegramFilesService : ITelegramFilesService
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _tableInitialized;


    public TelegramFilesService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> GetFileIdAsync(string trackUrl)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureTableAsync(connection);

        await using var command = new NpgsqlCommand(
            "SELECT file_ids -> @track_url FROM telegram_files WHERE id = 1", connection);

        command.Parameters.AddWithValue("track_url", trackUrl);

        return await command.ExecuteScalarAsync() as string;
    }

    public async Task SetFileIdAsync(string trackUrl, string fileId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureTableAsync(connection);

        await using var command = new NpgsqlCommand(
            "INSERT INTO telegram_files (id, file_ids) VALUES (1, hstore(@track_url, @file_id)) " +

            "ON CONFLICT (id) DO UPDATE SET file_ids = telegram_files.file_ids || EXCLUDED.file_ids",
            connection);
        command.Parameters.AddWithValue("track_url", trackUrl);
        command.Parameters.AddWithValue("file_id", fileId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureTableAsync(NpgsqlConnection connection)
    {
        if (_tableInitialized)
            return;

        await _schemaLock.WaitAsync();
        try
        {
            if (_tableInitialized)
                return;

            await using var command = new NpgsqlCommand(
                "CREATE EXTENSION IF NOT EXISTS hstore; " +
                "CREATE TABLE IF NOT EXISTS telegram_files (id smallint PRIMARY KEY, file_ids hstore NOT NULL)",
                connection);
            await command.ExecuteNonQueryAsync();
            _tableInitialized = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

}


public class MockTelegramFilesService : ITelegramFilesService
{
    public Task<string> GetFileIdAsync(string trackUrl)
    {
        return Task.FromResult<string>(null);
    }

    public Task SetFileIdAsync(string trackUrl, string fileId)
    {
        return Task.CompletedTask;
    }
}
