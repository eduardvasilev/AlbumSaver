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
        public async Task<IActionResult> Get(string query, int page)
        {
           return Ok(await _telegramService.Search(query, page));
        }

        [HttpPost("/download")]
        public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId)
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
    }
}
