using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public class TelegramService : ITelegramService
    {
        private readonly IUpdateService _updateService;
        private readonly IBotService _botService;
        private readonly YoutubeClient _youtubeClient;

        private const int ElementsPerPageCount = 8;

        public TelegramService(IUpdateService updateService, IBotService botService)
        {
            _updateService = updateService;
            _botService = botService;
            _youtubeClient = new YoutubeClient();
        }

        public async Task<IEnumerable<YTMusicSearchResult>> Search(string query, int page,
            CancellationToken cancellationToken)
        {
            return (await _youtubeClient.Search
                    .GetPlaylistsAsync(query, cancellationToken))
                .Skip(ElementsPerPageCount * page)
                .Take(ElementsPerPageCount)
                .Select(result => new YTMusicSearchResult
                {
                    ImageUrl = result.Thumbnails.Last().Url,
                    Title = result.Title,
                    YouTubeMusicPlaylistUrl = result.Url
                }).ToList();
        }

        public async Task SendAlbumAsync(DownloadRequest request)
        {
            Playlist result =
                (await _youtubeClient.Playlists.GetAsync(PlaylistId.Parse(request.YouTubeMusicPlaylistUrl)));

            var videos =
                await _youtubeClient.Playlists.GetVideosAsync(PlaylistId.Parse(request.YouTubeMusicPlaylistUrl));

            string thumbnail = result.Thumbnails.LastOrDefault()?.Url;

            if (videos.Any())
            {
                if (!string.IsNullOrWhiteSpace(thumbnail))
                {
                    await _botService.Client.SendPhotoAsync(request.UserId,
                        new InputOnlineFile(thumbnail));
                }

                foreach (PlaylistVideo playlistVideo in videos)
                {
                    await _updateService.SendSongAsync(request.UserId, playlistVideo, result.Thumbnails.LastOrDefault()?.Url);
                }
            }
        }
    }
}
