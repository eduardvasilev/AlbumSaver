using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YTMusicAPI.Model;
using YTMusicAPI.Model.Infrastructure;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services;
using EntityType = YTMusicDownloader.WebApi.Model.EntityType;

namespace YTMusicDownloader.WebApi.Controllers.V2
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiVersion(2)]
    public class TelegramWebController : ControllerBase 
    { 
        private readonly ISearchService _searchService;
        private readonly ITracksService _tracksService;
        private readonly IArtistsService _artistsService;
        private readonly IDownloadService _downloadService;
        private readonly IBotService _botService;
        private readonly TelemetryClient _telemetryClient;

        public TelegramWebController(ISearchService searchService,
            ITracksService tracksService, IArtistsService artistsService, IDownloadService downloadService, IBotService botService, TelemetryClient telemetryClient)
        {
            _searchService = searchService;
            _tracksService = tracksService;
            _artistsService = artistsService;
            _downloadService = downloadService;
            _botService = botService;
            _telemetryClient = telemetryClient;
        }

        [HttpGet("/search")]
        [HttpGet("/albums")]
        public async Task<IActionResult> Get(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
           return Ok(await _searchService.SearchAlbumsAsync(new QueryRequest
           {
               Query = query,
               ContinuationData = new ContinuationData(continuationToken, token),
               ContinuationNeed = continuation
           }, cancellationToken));
        }


        [HttpGet("/tracks")]
        public async Task<IActionResult> Tracks(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _searchService.SearchTracksAsync(new QueryRequest
            {
                Query = query,
                ContinuationData = new ContinuationData(continuationToken, token),
                ContinuationNeed = continuation
            }, cancellationToken));
        }

        [HttpGet("/album-tracks")]
        [HttpGet("/album/tracks")]
        public async Task<IActionResult> TracksByAlbum(string albumUrl, CancellationToken cancellationToken)
        {

            var tracks = await _tracksService.GetAlbumTracksAsync(albumUrl, cancellationToken);
            var result = new AlbumTracksResultObject<IEnumerable<MusicSearchResult>>(tracks.Result.Select(x => new MusicSearchResult
            {
                Author = x.Author.ToString(),
                ImageUrl = x.ImageUrl,
                Title = x.Title,
                YouTubeMusicPlaylistUrl = x.YouTubeMusicPlaylistUrl,
            }));

            var track = tracks.Result.FirstOrDefault();
            var youtube = new YoutubeClient();
            var album = await youtube.Playlists.GetAsync(PlaylistId.Parse(albumUrl));
            result.AlbumImage = album.Thumbnails.Skip(1).FirstOrDefault()?.Url;
            result.AlbumImageConst = track?.ImageUrl;
            string title = album.Title;
            string albumPrefix = "Album - ";
            if (title != null && title.StartsWith(albumPrefix))
                title = title.Substring(albumPrefix.Length);
            result.AlbumTitle = title;
            result.ArtistName = track?.Author;
            return Ok(result);
        }

        //TODO migrate to lib
        //[HttpGet("/artist/tracks")]
        //[HttpGet("/artist-tracks")]
        //[ResponseCache(Duration = 43200)]
        //public async Task<IActionResult> TracksByArtist(string channelUrl, bool continuation,
        //    string continuationToken,
        //    string token, int? takeCount, CancellationToken cancellationToken)
        //{
        //    var tracksByArtistAsync = await _telegramService.GetTracksByArtistAsync(channelUrl, continuation, continuationToken, token, cancellationToken);

        //    if (takeCount.HasValue)
        //    {
        //        //be careful with pagination. Tracks could be skipped
        //        tracksByArtistAsync.Result = tracksByArtistAsync.Result.Take(takeCount.Value);
        //    }

        //    return Ok(tracksByArtistAsync);
        //}

        [HttpGet("/releases")]
        [ResponseCache(Duration = 43200)]
        public async Task<IActionResult> Releases(CancellationToken cancellationToken)
        {
            return Ok(await _searchService.GetReleasesAsync(cancellationToken));
        }

        [HttpPost("/download")]
        public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId, CancellationToken cancellationToken,
            EntityType entityType = EntityType.Album)
        {
            try
            {
                switch (entityType)
                {
                    case EntityType.Album:
                    {
                        //TODO remove this workaround. Model should be passed in body
                        var model = new DownloadRequest
                        {
                            UserId = userId,
                            YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
                        };
                        _downloadService.SendAlbumAsync(model, cancellationToken);
                        break;
                    }
                    case EntityType.Track:
                    {
                        //TODO remove this workaround. Model should be passed in body
                        var model = new DownloadRequest
                        {
                            UserId = userId,
                            YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
                        };
                        _downloadService.SendTrackAsync(model, cancellationToken);
                        break;
                    }
                }

                return Ok();
            }
            catch(Exception) 
            {
                await _botService.Client.SendTextMessageAsync(new ChatId(userId),
                    "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

                return Ok();
            }
        }

        [HttpPost("/download-set")]
        public async Task<IActionResult> DownloadSet([FromBody] DownloadSetRequest request, CancellationToken cancellationToken)
        {
            _downloadService.SendTracksSetAsync(request, cancellationToken);

            return Ok();
        }

        [HttpGet("/artists")]
        public async Task<IActionResult> GetArtists(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _searchService.SearchArtistsAsync(new QueryRequest
            {
                Query = query,
                ContinuationData = new ContinuationData(continuationToken, token),
                ContinuationNeed = continuation
            }, cancellationToken));
        }


        //[HttpGet("/artists/albums")]
        //public async Task<IActionResult> GetArtistAlbums(string channelUrl, int? takeCount, CancellationToken cancellationToken)
        //{
        //    var albumsByArtistAsync = await _telegramService.GetAlbumsByArtistAsync(channelUrl, cancellationToken);
        //    if (takeCount.HasValue)
        //    {
        //        albumsByArtistAsync.Result = albumsByArtistAsync.Result.Take(takeCount.Value);
        //    }
        //    return Ok(albumsByArtistAsync);
        //}  
        
        [HttpGet("/artists/image")]
        public async Task<IActionResult> GetArtistImage(string channelUrl, CancellationToken cancellationToken)
        {
            return Ok(await _artistsService.GetArtistImageAsync(channelUrl, cancellationToken));
        }


        [HttpPost("/callback")]

        public async Task<IActionResult> Post([FromBody] object request, CancellationToken cancellationToken)
        {
            Update? update;
            try
            {
                update = JsonConvert.DeserializeObject<Update>(request.ToString());

            }
            catch (Exception)
            {
                return Ok();
            }

            if (update is { PreCheckoutQuery: { } })
            {
                var preCheckoutQuery = update.PreCheckoutQuery;
                await _botService.Client.AnswerPreCheckoutQueryAsync(
                    preCheckoutQueryId: preCheckoutQuery.Id, cancellationToken: cancellationToken);
                await _botService.Client.SendTextMessageAsync(-911492578, $"Donate from @{preCheckoutQuery.From.Username}: \n\r{preCheckoutQuery.TotalAmount} {preCheckoutQuery.Currency}", cancellationToken: cancellationToken);

            }
            //call and forget
            return Ok();
        }
    }
}
