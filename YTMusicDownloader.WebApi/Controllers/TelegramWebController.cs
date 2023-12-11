using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Telegram.Bot;
using Telegram.Bot.Types;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services;

namespace YTMusicDownloader.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelegramWebController : ControllerBase 
    { 
        private readonly ITelegramService _telegramService;
        private readonly IBotService _botService;
        private readonly TelemetryClient _telemetryClient;

        public TelegramWebController(ITelegramService telegramService, IBotService botService, TelemetryClient telemetryClient)
        {
            _telegramService = telegramService;
            _botService = botService;
            _telemetryClient = telemetryClient;
        }

        [HttpGet("/search")]
        [HttpGet("/albums")]
        public async Task<IActionResult> Get(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
           return Ok(await _telegramService.Search(query, continuation, continuationToken, token, cancellationToken));
        }


        [HttpGet("/tracks")]
        public async Task<IActionResult> Tracks(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.SearchTracks(query, continuation, continuationToken, token, cancellationToken));
        }

        [HttpGet("/album-tracks")]
        [HttpGet("/album/tracks")]
        public async Task<IActionResult> TracksByAlbum(string albumUrl, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetTracksByAlbumAsync(albumUrl, cancellationToken));
        }

        [HttpGet("/artist/tracks")]
        [HttpGet("/artist-tracks")]
        [ResponseCache(Duration = 43200)]
        public async Task<IActionResult> TracksByArtist(string channelUrl, bool continuation,
            string continuationToken,
            string token, int? takeCount, CancellationToken cancellationToken)
        {
            var tracksByArtistAsync = await _telegramService.GetTracksByArtistAsync(channelUrl, continuation, continuationToken, token, cancellationToken);

            if (takeCount.HasValue)
            {
                //be careful with pagination. Tracks could be skipped
                tracksByArtistAsync.Result = tracksByArtistAsync.Result.Take(takeCount.Value);
            }

            return Ok(tracksByArtistAsync);
        }

        [HttpGet("/releases")]
        [ResponseCache(Duration = 43200)]
        public async Task<IActionResult> Releases(string query, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetReleases());
        }

        [HttpPost("/download")]
        public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId,
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
                        _telegramService.SendAlbumAsync(model);
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
                        _telegramService.SendTrackAsync(model);
                        break;
                    }
                }

                return Ok();
            }
            catch(Exception exception) 
            {
                await _botService.Client.SendTextMessageAsync(new ChatId(userId),
                    "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

                return Ok();
            }

            return Ok();
        }

        [HttpPost("/download-set")]
        public async Task<IActionResult> DownloadSet([FromBody] DownloadSetRequest request)
        {
            //try
            //{
                _telegramService.SendTracksSetAsync(request);

                return Ok();
            //}
            //catch
            //{
            //    await _botService.Client.SendTextMessageAsync(new ChatId(request.UserId),
            //        "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

            //    return Ok();
            //}
        }

        [HttpGet("/artists")]
        public async Task<IActionResult> GetArtists(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetArtists(query, continuation, continuationToken, token, cancellationToken));
        }


        [HttpGet("/artists/albums")]
        public async Task<IActionResult> GetArtistAlbums(string channelUrl, int? takeCount, CancellationToken cancellationToken)
        {
            var albumsByArtistAsync = await _telegramService.GetAlbumsByArtistAsync(channelUrl, cancellationToken);
            if (takeCount.HasValue)
            {
                albumsByArtistAsync.Result = albumsByArtistAsync.Result.Take(takeCount.Value);
            }
            return Ok(albumsByArtistAsync);
        }  
        
        [HttpGet("/artists/image")]
        public async Task<IActionResult> GetArtistImage(string channelUrl, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetArtistImageAsync(channelUrl, cancellationToken));
        }
    }
}
