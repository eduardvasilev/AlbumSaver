using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using YoutubeExplode.Videos;

namespace YTMusicDownloader.WebApi.Services
{
    public interface IUpdateService
    {
        Task ProcessAsync(Update update, CancellationToken cancellationToken);

        Task SendSongAsync(long chatId, IVideo video, InputMedia thump,
            CancellationToken cancellationToken = default);
    }
}
