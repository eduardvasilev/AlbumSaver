using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using YoutubeExplode.Videos;
using YTMusicDownloader.WebApi.Services;

namespace YTMusicDownloader.WebApi.Controllers;

[Route("api/[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebPlayerController : Controller
{
    private readonly ITelegramService _telegramService;
    private readonly IUpdateService _updateService;

    public WebPlayerController(ITelegramService telegramService, IUpdateService updateService)
    {
        _telegramService = telegramService;
        _updateService = updateService;
    }
    // GET
    public async Task<IActionResult> Index(string trackId, CancellationToken cancellation)
    {
        Stream audioStreamAsync = await _updateService.GetAudioStreamAsync(VideoId.Parse(trackId), cancellation);
        return new FileStreamResult(audioStreamAsync, "audio/webm");
    }
}