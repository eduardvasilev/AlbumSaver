using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services;

namespace YTMusicDownloader.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelegramWebController : ControllerBase 
    { 
        private readonly ITelegramService _telegramService;
        public TelegramWebController(ITelegramService telegramService)
        {
            _telegramService = telegramService;
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

        [HttpGet("/releases")]
        public async Task<IActionResult> Releases(string query, CancellationToken cancellationToken)
        {
            return Ok(await _telegramService.GetReleases(cancellationToken));
        }

        [HttpPost("/download")]
        public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId, EntityType entityType = EntityType.Album)
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

            return BadRequest();
        }
    }
}
