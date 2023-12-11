using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YTMusicDownloader.WebApi.Services;


var serviceProvider = new ServiceCollection()
    .AddHttpClient()
    .BuildServiceProvider();

var factory = (IHttpClientFactory)serviceProvider.GetService(typeof(IHttpClientFactory));

var botClient = new TelegramBotClient("");

UpdateService updateService = new UpdateService(new BotService()
{
    Client = botClient
}, factory, new TelemetryClient());

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
};


botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    CancellationToken.None
);

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
   await updateService.ProcessAsync(update, cancellationToken);
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

Console.ReadKey();