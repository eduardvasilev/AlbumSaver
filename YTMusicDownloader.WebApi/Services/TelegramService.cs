using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public class TelegramService : ITelegramService
    {
        private readonly IUpdateService _updateService;
        private readonly IBotService _botService;
        private readonly YoutubeClient _youtubeClient;

        private const int ElementsPerPageCount = 8;
        private const int SongsPerPageCount = 10;

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
                    Author = result.Author?.ToString(),
                    YouTubeMusicPlaylistUrl = result.Url
                }).ToList();
        }

        public async Task<IEnumerable<YTMusicSearchResult>> SearchTracks(string query, int page,
            CancellationToken cancellationToken)
        {
            var videoSearchResults = (await _youtubeClient.Search
                .GetVideosAsync(query, cancellationToken));
            return videoSearchResults
                .Skip(SongsPerPageCount * page)
                .Take(SongsPerPageCount)
                .Select(result => new YTMusicSearchResult
                {
                    ImageUrl = result.Thumbnails.LastOrDefault()?.Url,
                    Title = result.Title,
                    Author = result.Author?.ToString(),
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
            InputMedia inputOnlineFile = new InputMedia(thumbnail);
            InputMedia thumb = new InputMedia(result.Thumbnails.FirstOrDefault()?.Url);

            if (videos.Any())
            {
                if (!string.IsNullOrWhiteSpace(thumbnail))
                {
                    await _botService.Client.SendPhotoAsync(request.UserId,
                        inputOnlineFile);
                }

                foreach (PlaylistVideo playlistVideo in videos)
                {
                    await _updateService.SendSongAsync(request.UserId, playlistVideo, thumb);
                }
            }
        }

        public async Task SendTrackAsync(DownloadRequest request)
        {
            var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(request.YouTubeMusicPlaylistUrl));

            InputMedia thumb = new InputMedia(result.Thumbnails.FirstOrDefault()?.Url);
            await _updateService.SendSongAsync(request.UserId, result, thumb);
        }
    }
}
