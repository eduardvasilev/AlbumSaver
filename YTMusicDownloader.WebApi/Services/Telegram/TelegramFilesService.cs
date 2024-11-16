using StackExchange.Redis;
using System.Threading.Tasks;

namespace YTMusicDownloader.WebApi.Services.Telegram;

public class TelegramFilesService : ITelegramFilesService
{
    private IDatabase _db;

    public TelegramFilesService()
    {
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("musicsaver_redis:6379");
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