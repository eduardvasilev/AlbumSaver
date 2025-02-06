using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Telegram.Bot;
using Telegram.Bot.Types;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services;
using Newtonsoft.Json;
using Telegram.Bot.Types.ReplyMarkups;

namespace YTMusicDownloader.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiVersion(1)]
    public class TelegramWebController : ControllerBase 
    { 
        private readonly ITelegramService _telegramService;
        private readonly IBotService _botService;
        private readonly TelemetryClient _telemetryClient;
        private readonly IDownloadService _downloadService;

        public TelegramWebController(ITelegramService telegramService, IBotService botService, TelemetryClient telemetryClient, IDownloadService downloadService)
        {
            _telegramService = telegramService;
            _botService = botService;
            _telemetryClient = telemetryClient;
            _downloadService = downloadService;
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

        //[HttpPost("/download")]
        //public async Task<IActionResult> Download(string youTubeMusicPlaylistUrl, long userId,
        //    EntityType entityType = EntityType.Album)
        //{
        //    try
        //    {
        //        switch (entityType)
        //        {
        //            case EntityType.Album:
        //            {
        //                //TODO remove this workaround. Model should be passed in body
        //                var model = new DownloadRequest
        //                {
        //                    UserId = userId,
        //                    YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
        //                };
        //                _telegramService.SendAlbumAsync(model);
        //                break;
        //            }
        //            case EntityType.Track:
        //            {
        //                //TODO remove this workaround. Model should be passed in body
        //                var model = new DownloadRequest
        //                {
        //                    UserId = userId,
        //                    YouTubeMusicPlaylistUrl = youTubeMusicPlaylistUrl
        //                };
        //                _telegramService.SendTrackAsync(model);
        //                break;
        //            }
        //        }

        //        return Ok();
        //    }
        //    catch(Exception exception) 
        //    {
        //        await _botService.Client.SendTextMessageAsync(new ChatId(userId),
        //            "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

        //        return Ok();
        //    }

        //    return Ok();
        //}

        //[HttpPost("/download-set")]
        //public async Task<IActionResult> DownloadSet([FromBody] DownloadSetRequest request)
        //{
        //    //try
        //    //{
        //        _telegramService.SendTracksSetAsync(request);

        //        return Ok();
        //    //}
        //    //catch
        //    //{
        //    //    await _botService.Client.SendTextMessageAsync(new ChatId(request.UserId),
        //    //        "We're sorry. Something went wrong during sending. Please try again or use /feedback command to describe your issue.");

        //    //    return Ok();
        //    //}
        //}

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
            catch (Exception)
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
                return Ok();
            }

            var inputText = update?.Message?.Text ?? update?.CallbackQuery?.Data;
            const string feedbackText = "Please describe your idea or issue.";
            if (inputText?.StartsWith("/feedback") == true)
            {

                await _botService.Client.SendTextMessageAsync(update?.Message?.Chat.Id ?? update.CallbackQuery?.Message.Chat.Id, feedbackText, replyMarkup:
                    new ForceReplyMarkup(), cancellationToken: cancellationToken);
                return Ok();
            }

            if (update?.Message?.ReplyToMessage != null && update?.Message?.ReplyToMessage.Text == feedbackText && update?.Message?.Text != null)
            {
                await _botService.Client.SendTextMessageAsync(-911492578, $"Feedback from @{update?.Message?.Chat.Username}: \n\r{update?.Message?.Text}", cancellationToken: cancellationToken);
                return Ok();
            }

            //call and forget
            return Ok();
        }
    }
}
