using System.Threading.Tasks;

namespace YTMusicDownloader.WebApi.Services.Telegram;

public interface ITelegramFilesService
{
    Task<string> GetFileIdAsync(string trackUrl);

    Task SetFileIdAsync(string trackUrl, string fileId);
}