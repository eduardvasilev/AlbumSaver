using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;

namespace YTMusicDownloader.WebApi.Services;

public class PaymentService : IPaymentService
{
    private readonly IBotService _botService;
    private readonly IOptions<PaymentOptions> _options;

    public PaymentService(IBotService botService, IOptions<PaymentOptions> options)
    {
        _botService = botService;
        _options = options;
    }
    public async Task SendDonateMessageAsync(long userId)
    {
        if (_options.Value.AskForDonate)
        {
            await _botService.Client.SendInvoiceAsync(userId, "Buy us a coffee",
                "Support us so we can add new features. Use /feedback command for your suggestions",
                Guid.NewGuid().ToString(), "", "XTR", new List<LabeledPrice>() { new("Donate us", 1)});
        }
    }
}

public class PaymentOptions
{
    public bool AskForDonate { get; set; }
}