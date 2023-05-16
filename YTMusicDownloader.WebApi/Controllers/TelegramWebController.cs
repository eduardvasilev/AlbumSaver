using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
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

        public TelegramWebController(ITelegramService telegramService, IBotService botService)
        {
            _telegramService = telegramService;
            _botService = botService;
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
        public async Task<IActionResult> TracksByAlbum(string albumUrl, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetTracksByAlbumAsync(albumUrl, cancellationToken));
        }

        [HttpGet("/artist-tracks")]
        public async Task<IActionResult> TracksByArtist(string channelUrl, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetTracksByArtistAsync(channelUrl, cancellationToken));
        }

        [HttpGet("/releases")]
        public async Task<IActionResult> Releases(string query, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetReleases());
        }

        [HttpPost("/download")]
        public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId, EntityType entityType = EntityType.Album)
        {
            try
            {
                if (entityType == EntityType.Album)
                {
                    //TODO remove this workaround. Model should be passed in body
                    var model = new DownloadRequest
                    {
                        UserId = userId,
                        YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
                    };
                    _telegramService.SendAlbumAsync(model);
                    return Ok();
                }
                else if (entityType == EntityType.Track)
                {
                    //TODO remove this workaround. Model should be passed in body
                    var model = new DownloadRequest
                    {
                        UserId = userId,
                        YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
                    };
                    _telegramService.SendTrackAsync(model);
                    return Ok();
                }
            }
            catch
            {
                await _botService.Client.SendTextMessageAsync(new ChatId(userId),
                    "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

                return BadRequest();
            }

            return BadRequest();
        }

        [HttpPost("/download-set")]
        public async Task<IActionResult> DownloadSet([FromBody] DownloadSetRequest request)
        {
            try
            {
                _telegramService.SendTracksSetAsync(request);

                return Ok();
            }
            catch
            {
                await _botService.Client.SendTextMessageAsync(new ChatId(request.UserId),
                    "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

                return BadRequest();
            }
        }

        [HttpGet("/artists")]
        public async Task<IActionResult> GetArtists(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetArtists(query, continuation, continuationToken, token, cancellationToken));
        }


        [HttpGet("/artists/albums")]
        public async Task<IActionResult> GetArtistAlbums(string channelUrl, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetAlbumsByArtistAsync(channelUrl, cancellationToken));
        }
    }
}
