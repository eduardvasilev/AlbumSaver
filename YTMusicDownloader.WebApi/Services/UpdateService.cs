using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YTMusicDownloader.WebApi.Model;
using Author = YoutubeExplode.Common.Author;

namespace YTMusicDownloader.WebApi.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly IBotService _botService;
        private readonly YoutubeClient _youtube;
        private readonly HttpClient _httpClient;

        public UpdateService(IBotService botService, IHttpClientFactory httpClientFactory)
        {
            _botService = botService;
            _youtube = new YoutubeClient();
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
        {
            var message = update.Message;

            bool isCallback = update.CallbackQuery != null;


            if (message?.Type == MessageType.Text || isCallback)
            {

                var chatId = update?.Message?.Chat?.Id ?? update.CallbackQuery.Message.Chat.Id;

                var inputText = update?.Message?.Text ?? update.CallbackQuery.Data;


                if (inputText == "/start" || string.IsNullOrWhiteSpace(inputText))
                {
                    return;
                }

                if (inputText == "/actual")
                {
                    await SendActualAsync(chatId, cancellationToken);
                    return;
                }

                if (inputText.StartsWith("/song"))
                {

                    await _botService.Client.SendTextMessageAsync(chatId, "Please, type a song name", replyMarkup:
                        new ForceReplyMarkup(), cancellationToken: cancellationToken);
                    return;
                }

                if (message?.ReplyToMessage != null && message.ReplyToMessage.Text == "Please, type a song name")
                {
                    await SendSongAsync(chatId, inputText, cancellationToken);
                    return;
                }

                await SendAlbumAsync(inputText, isCallback, message, chatId, cancellationToken);
            }
        }

        private async Task SendAlbumAsync(string inputText, bool isCallback, Message message, long chatId,
            CancellationToken cancellationToken)
        {
            if (!isCallback)
            {
                await SendSearchResilt(inputText, chatId, 0, cancellationToken);
            }
            else
            {
                CallbackItem deserializeObject = null;
                try
                {
                    deserializeObject = JsonConvert.DeserializeObject<CallbackItem>(inputText);
                }
                catch
                {
                    //
                }

                if (deserializeObject?.P == true || deserializeObject?.N == true)
                {
                    if (deserializeObject.N)
                    {
                        await SendSearchResilt(deserializeObject.Name, chatId, --deserializeObject.Page, cancellationToken);
                        return;
                    }

                    await SendSearchResilt(deserializeObject.Name, chatId, ++deserializeObject.Page, cancellationToken);
                    return;
                }

                //todo remove this govnocode
                CallbackItem callbackItem = deserializeObject ?? new CallbackItem
                {
                    Index = 0
                };

                var result = (await _youtube.Search.GetPlaylistsAsync(callbackItem?.Name ?? inputText, cancellationToken))
                    .Skip(callbackItem.Page * 6).Take(6).ToList();

                PlaylistSearchResult playlistSearchResult = result.ElementAtOrDefault(callbackItem.Index) ?? result.FirstOrDefault();

                if (playlistSearchResult != null)
                {
                    var videos = await _youtube.Playlists.GetVideosAsync(PlaylistId.Parse(playlistSearchResult.Url),
                        cancellationToken);

                    if (videos.Count > 20 && message == null)
                    {
                        await _botService.Client.SendTextMessageAsync(chatId,
                            "You're trying to get more than 20 tracks. Are you sure?", replyMarkup:
                            new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Yes!", inputText)), cancellationToken: cancellationToken);
                        return;
                    }

                    string thumbnail = playlistSearchResult.Thumbnails.LastOrDefault()?.Url;

                    if (videos.Any())
                    {
                        if (!string.IsNullOrWhiteSpace(thumbnail))
                        {
                            await _botService.Client.SendPhotoAsync(chatId, new InputOnlineFile(thumbnail), cancellationToken: cancellationToken, disableNotification: true);
                        }

                        foreach (PlaylistVideo playlistVideo in videos)
                        {
                            await SendSongAsync(chatId, playlistVideo, null, cancellationToken);
                        }
                    }
                }
            }
        }

        private async Task SendSearchResilt(string inputText, long chatId, int page, CancellationToken cancellationToken)
        {
            IReadOnlyList<PlaylistSearchResult> playlistSearchResults = (await _youtube.Search.GetPlaylistsAsync(inputText, cancellationToken));
            var result = playlistSearchResults
                .Skip(page*6).Take(6).ToList();

            string albumsMessage = string.Empty;
            var count = result.Count < 6 ? result.Count : 6;
            for (var index = 0; index < count; index++)
            {
                var album = result[index];
                albumsMessage += $"{index + 1}. {album.Title} \n\r";
            }

            var inlineKeyboardButtons = result
                .Select((x, index) =>
                    InlineKeyboardButton.WithCallbackData((index + 1).ToString(),
                        JsonConvert.SerializeObject(new CallbackItem()
                        {
                            Index = index,
                            Name = inputText,
                            Page = page
                        }))).ToList();

            if (result.Count >= 6)
            {
                inlineKeyboardButtons = inlineKeyboardButtons.Append(InlineKeyboardButton.WithCallbackData(">", JsonConvert.SerializeObject(new CallbackItem()
                {
                    Index = -2,
                    Name = inputText,
                    P = true,
                    Page = page
                }))).ToList();
            }

            if (page != 0)
            {
                inlineKeyboardButtons = inlineKeyboardButtons.Prepend(InlineKeyboardButton.WithCallbackData("<", JsonConvert.SerializeObject(new CallbackItem()
                {
                    Index = -1,
                    Name = inputText,
                    N = true,
                    Page = page
                }))).ToList();
            }

            await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                new InlineKeyboardMarkup(inlineKeyboardButtons), cancellationToken: cancellationToken);
        }

        private async Task SendSongAsync(long chatId, string songName, CancellationToken cancellationToken)
        {
            var songs = await _youtube.Search.GetVideosAsync(songName, cancellationToken);

            VideoSearchResult videoSearchResult = songs.FirstOrDefault();
            {
                if (videoSearchResult != null)
                {
                    await SendSongAsync(chatId, videoSearchResult, null,  cancellationToken);
                }
            }
        }

        public async Task SendSongAsync(long chatId, IVideo video, string thump, CancellationToken cancellationToken)
        {
            var videoId = VideoId.Parse(video.Id);

            Stream stream = await GetAudioStreamAsync(videoId, cancellationToken);

            await SendAudioAsync(chatId, stream, video.Title, video.Duration, thump, video.Author, cancellationToken);
        }

        private async Task SendActualAsync(long chatId, CancellationToken cancellationToken)
        {
            HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync(
                "https://rss.applemarketingtools.com/api/v2/us/music/most-played/10/albums.json",
                cancellationToken);

            ItunesMostRecentModel? results = await httpResponseMessage.Content.ReadFromJsonAsync<ItunesMostRecentModel>(cancellationToken: cancellationToken);

            string albumsMessage = string.Empty;
            var count = (results?.Feed.Results.Count ?? 0) < 6 ? results.Feed.Results.Count : 6;
            for (var index = 0; index < count; index++)
            {
                var album = results.Feed.Results[index];
                albumsMessage += $"{index + 1}. {album.ArtistName} - {album.Name} \n\r";
            }

            await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                new InlineKeyboardMarkup(results.Feed.Results
                    .Select((x, index) =>
                        InlineKeyboardButton.WithCallbackData((index + 1).ToString(),
                        $"{x.ArtistName} {x.Name}"))), cancellationToken: cancellationToken);
        }

        private async Task SendAudioAsync(long chatId, Stream stream, string title, TimeSpan? videoDuration,
            string thump,
            Author videoAuthor,
            CancellationToken cancellationToken)
        {
            await _botService.Client.SendAudioAsync(chatId, new InputMedia(stream, title), 
                cancellationToken: cancellationToken,
                duration: (videoDuration.HasValue ? (int?) videoDuration.Value.TotalSeconds : null), parseMode: ParseMode.Html, thumb:  thump, title: title, disableNotification: true, performer: videoAuthor.ChannelTitle);
            
        }

        private async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
        {
            StreamManifest streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

            IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            return await _youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        }
    }
}
