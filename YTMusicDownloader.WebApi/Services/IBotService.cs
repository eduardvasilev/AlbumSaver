using Telegram.Bot;

namespace YTMusicDownloader.WebApi.Services
{
    public interface IBotService
    {
        TelegramBotClient Client { get; }
    }
}
