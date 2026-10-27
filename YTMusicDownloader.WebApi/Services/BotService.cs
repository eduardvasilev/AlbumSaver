﻿using System;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Telegram.Bot;


namespace YTMusicDownloader.WebApi.Services
{
    public class BotService : IBotService
    {
        public BotService()
        {
            
        }

        public BotService(IOptions<BotConfiguration> config)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            Client = new TelegramBotClient(config.Value.BotToken, httpClient)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

        }


        public TelegramBotClient Client { get; set; }
    }
}
