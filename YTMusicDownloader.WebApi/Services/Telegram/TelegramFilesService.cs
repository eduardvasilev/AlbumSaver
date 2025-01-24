using StackExchange.Redis;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace YTMusicDownloader.WebApi.Services.Telegram;

public class TelegramFilesService : ITelegramFilesService
{
    private IDatabase _db;

    public TelegramFilesService(IOptions<RedisOptions> redisOptions)
    {
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisOptions.Value.Connection);
        _db = redis.GetDatabase();
    }

    public async Task<string> GetFileIdAsync(string trackUrl)
    {
        return await _db.StringGetAsync(trackUrl);
    }

    public async Task SetFileIdAsync(string trackUrl, string fileId)
    {
        await _db.StringSetAsync(trackUrl, fileId);
    }
}

public class MockTelegramFilesService : ITelegramFilesService
{
    public Task<string> GetFileIdAsync(string trackUrl)
    {
        return null;
    }

    public Task SetFileIdAsync(string trackUrl, string fileId)
    {
        return Task.CompletedTask;
    }
}
