using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using YoutubeExplode.Videos;

namespace YTMusicDownloader.WebApi.Services
{
    public interface IUpdateService
    {
        Task ProcessAsync(Update update, CancellationToken cancellationToken);

        Task SendSongAsync(long chatId, IVideo video, InputFileUrl thump,
            CancellationToken cancellationToken = default);

        Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken);
    }
}
