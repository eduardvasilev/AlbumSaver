using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using YTMusicDownloader.WebApi.Services;

namespace YTMusicDownloader.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class UpdateController : Controller
    {
        private readonly IUpdateService _updateService;
        private readonly IBotService _botService;

        public UpdateController(IUpdateService updateService, IBotService botService)
        {
            _updateService = updateService;
            _botService = botService;
        }

        // POST api/update
        [HttpPost]

        public async Task<IActionResult> Post([FromBody] Update request, CancellationToken cancellationToken)
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
