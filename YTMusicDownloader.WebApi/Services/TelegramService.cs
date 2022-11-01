using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using YoutubeExplode;
using YoutubeExplode.Bridge;
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

        public TelegramService(IUpdateService updateService, IBotService botService)
        {
            _updateService = updateService;
            _botService = botService;
            _youtubeClient = new YoutubeClient();
        }

        public async Task<PagingResult> Search(string query,
            bool continuation, 
            string continuationToken,
            string token,
            CancellationToken cancellationToken)
        {

            ContinuationData continuationData = continuation ? new ContinuationData
            {
                ContinuationToken = new JValue(continuationToken),
                Token = new JValue(token)
            } : null;

            var playlistSearchResults = await _youtubeClient.Search
                .GetPlaylistsAsync(query, continuation, continuationData, cancellationToken);

            return new PagingResult
            {
                Result = playlistSearchResults
                    .Select(result => new YTMusicSearchResult
                    {
                        ImageUrl = result.Thumbnails.Last().Url,
                        Title = result.Title,
                        Author = result.Author?.ToString(),
                        YouTubeMusicPlaylistUrl = result.Url,

                    }).ToList(),
                Token = _youtubeClient.Search.Token != null ? _youtubeClient.Search.Token.Value<string>() : null,
                ContinuationToken = _youtubeClient.Search.ContinuationToken != null
                    ? _youtubeClient.Search.ContinuationToken.Value<string>()
                    : null,
            };
        }

        public async Task<PagingResult> SearchTracks(string query,
            bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken)
        {

            ContinuationData continuationData = continuation ? new ContinuationData
            {
                ContinuationToken = new JValue(continuationToken),
                Token = new JValue(token)
            } : null;

            var videoSearchResults = (await _youtubeClient.Search
                .GetVideosAsync(query, continuation, continuationData, cancellationToken));

            return new PagingResult
            {
                Result = videoSearchResults
                    .Select(result => new YTMusicSearchResult
                    {
                        ImageUrl = result.Thumbnails.LastOrDefault()?.Url,
                        Title = result.Title,
                        Author = result.Author?.ToString(),
                        YouTubeMusicPlaylistUrl = result.Url,
                    }).ToList(),
                Token = _youtubeClient.Search.Token != null ? _youtubeClient.Search.Token.Value<string>() : null,
                ContinuationToken = _youtubeClient.Search.ContinuationToken != null
                    ? _youtubeClient.Search.ContinuationToken.Value<string>()
                    : null,
            };
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
