using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using YoutubeExplode.Videos;

namespace YTMusicDownloader.WebApi.Services
{
    public interface IUpdateService
    {
        Task ProcessAsync(Update update, CancellationToken cancellationToken);

        Task SendSongAsync(long chatId, IVideo video, string thump, CancellationToken cancellationToken = default);
    }
}
