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
    [ApiVersion(2)]
    public class TelegramWebController : ControllerBase 
    { 
        private readonly IBotService _botService;

        public TelegramWebController(IBotService botService)
        {
            _botService = botService;
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
