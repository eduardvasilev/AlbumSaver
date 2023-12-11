using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YTMusicDownloader.WebApi.Model;
using Author = YoutubeExplode.Common.Author;
using YoutubeExplode.Exceptions;
using YTMusicAPI;
using YTMusicAPI.Model.Domain;
using Microsoft.ApplicationInsights;

namespace YTMusicDownloader.WebApi.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly IBotService _botService;
        private readonly TelemetryClient _telemetryClient;
        private readonly YoutubeClient _youtube;
        private readonly HttpClient _httpClient;

        public UpdateService(IBotService botService, IHttpClientFactory httpClientFactory, TelemetryClient telemetryClient)
        {
            _botService = botService;
            _telemetryClient = telemetryClient;

            var baseAddress = new Uri("https://music.youtube.com");
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(baseAddress, new Cookie("CONSENT", "YES+"));
            cookieContainer.Add(baseAddress, new Cookie("SOCS", "CAISNQgDEitib3FfaWRlbnRpdHlmcm9udGVuZHVpc2VydmVyXzIwMjMwODI5LjA3X3AxGgJlbiACGgYIgLC_pwY"));
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer
            };
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
            _httpClient = new HttpClient(handler, false);

            _youtube = new YoutubeClient(_httpClient);
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
                
                if (inputText.StartsWith("/feedback"))
                {

                    await _botService.Client.SendTextMessageAsync(chatId, "Please describe your idea or issue.", replyMarkup:
                        new ForceReplyMarkup(), cancellationToken: cancellationToken);
                    return;
                }

                if (message?.ReplyToMessage != null && message.ReplyToMessage.Text == "Please, type a song name")
                {
                    await SendSongAsync(chatId, inputText, cancellationToken);
                    return;
                }   
                
                if (message?.ReplyToMessage != null && message.ReplyToMessage.Text == "Please describe your idea or issue." && message.Text != null)
                {
                    await _botService.Client.SendTextMessageAsync(-911492578, $"Feedback from @{message.Chat.Username}: \n\r{message.Text}", cancellationToken: cancellationToken);
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
                        await SendSearchResilt(deserializeObject.Na, chatId, --deserializeObject.Pg, cancellationToken);
                        return;
                    }

                    await SendSearchResilt(deserializeObject.Na, chatId, ++deserializeObject.Pg, cancellationToken);
                    return;
                }

                //todo remove this govnocode
                CallbackItem callbackItem = deserializeObject ?? new CallbackItem
                {
                    I = 0
                };

                var result = (await _youtube.Search.GetPlaylistsAsync(callbackItem.Na ?? inputText, false, null, cancellationToken))
                    .Skip(callbackItem.Pg * 5).Take(5).ToList();

                PlaylistSearchResult playlistSearchResult = result.ElementAtOrDefault(callbackItem.I) ?? result.FirstOrDefault();

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
                            await _botService.Client.SendPhotoAsync(chatId, new InputFileUrl(thumbnail), cancellationToken: cancellationToken);
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
            IReadOnlyList<PlaylistSearchResult> playlistSearchResults = (await _youtube.Search.GetPlaylistsAsync(inputText, false, null, cancellationToken));
            var result = playlistSearchResults
                .Skip(page * 5).Take(5).ToList();

            string albumsMessage = string.Empty;
            var count = result.Count < 5 ? result.Count : 5;
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
                            I = index,
                            Na = inputText,
                            Pg = page
                        }, new JsonSerializerSettings
                        {

                        }))).ToList();

            if (result.Count >= 5)
            {
                inlineKeyboardButtons = inlineKeyboardButtons.Append(InlineKeyboardButton.WithCallbackData(">", JsonConvert.SerializeObject(new CallbackItem()
                {
                    I = -2,
                    Na = inputText,
                    P = true,
                    Pg = page
                }))).ToList();
            }

            if (page != 0)
            {
                inlineKeyboardButtons = inlineKeyboardButtons.Prepend(InlineKeyboardButton.WithCallbackData("<", JsonConvert.SerializeObject(new CallbackItem()
                {
                    I = -1,
                    Na = inputText,
                    N = true,
                    Pg = page
                }))).ToList();
            }

            try
            {
                await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                    new InlineKeyboardMarkup(inlineKeyboardButtons), cancellationToken: cancellationToken);
            }
            catch (ApiRequestException exception) when (exception.Message == "Bad Request: BUTTON_DATA_INVALID")
            {
                await SendFallback(inputText, chatId, cancellationToken, albumsMessage, inlineKeyboardButtons);
            }
        }

        private async Task SendFallback(string inputText, long chatId, CancellationToken cancellationToken,
            string albumsMessage, List<InlineKeyboardButton> inlineKeyboardButtons)
        {
            List<InlineKeyboardButton> newButtons = new List<InlineKeyboardButton>();
            foreach (var inlineKeyboardButton in inlineKeyboardButtons)
            {
                CallbackItem deserializeObject = JsonConvert.DeserializeObject<CallbackItem>(inlineKeyboardButton.CallbackData);
                deserializeObject.Na = deserializeObject.Na.Substring(0, deserializeObject.Na.Length - 3);
                string serializeObject = JsonConvert.SerializeObject(deserializeObject);
                InlineKeyboardButton withCallbackData = InlineKeyboardButton.WithCallbackData(inlineKeyboardButton.Text, serializeObject);
                newButtons.Add(withCallbackData);
            }

            try
            {
                var serializeObject = JsonConvert.SerializeObject(new CallbackItem()
                {
                    I = 0,
                    Na = inputText,
                    Pg = 0
                });
                await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                    new InlineKeyboardMarkup(newButtons),
                    cancellationToken: cancellationToken);
            }
            catch (ApiRequestException exception) when (exception.Message == "Bad Request: BUTTON_DATA_INVALID")
            {
                await SendFallback(inputText, chatId, cancellationToken, albumsMessage,
                    inlineKeyboardButtons);
            }

        }
        private async Task SendSongAsync(long chatId, string songName, CancellationToken cancellationToken)
        {
            var songs = await _youtube.Search.GetVideosAsync(songName, false, null, cancellationToken);

            VideoSearchResult videoSearchResult = songs.FirstOrDefault();
            {
                if (videoSearchResult != null)
                {
                    await SendSongAsync(chatId, videoSearchResult, null,  cancellationToken);
                }
            }
        }

        public async Task SendSongAsync(long chatId, IVideo video, InputFileUrl thump,
            CancellationToken cancellationToken)
        {
            try
            {
                await SendSongInternalAsync(chatId, video, thump);
            }
         
            catch(Exception exception)
            {
                _telemetryClient.TrackException(exception);

                await _botService.Client.SendTextMessageAsync(chatId,
                    $"Sorry, we couldn't send the track: {video.Title}. Please try again.");
            }
        }

        private async Task SendSongInternalAsync(long chatId, IVideo video, InputFileUrl thump)
        {
            var videoId = VideoId.Parse(video.Id);

            await using (Stream stream = await GetAudioStreamAsync(videoId, CancellationToken.None))
            {
                await _botService.Client.SendAudioAsync(chatId, new InputFileStream(stream, video.Title), 
                    cancellationToken: CancellationToken.None,
                    duration: (video.Duration.HasValue ? (int?) video.Duration.Value.TotalSeconds : null),
                    parseMode: ParseMode.Html, thumbnail:  thump, title: video.Title, disableNotification: true, performer: video.Author.ChannelTitle);
            }
     
        }

        private async Task SendActualAsync(long chatId, CancellationToken cancellationToken)
        {
            HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync(
                "https://rss.applemarketingtools.com/api/v2/us/music/most-played/10/albums.json",
                cancellationToken);

            ItunesMostRecentModel? results = await httpResponseMessage.Content.ReadFromJsonAsync<ItunesMostRecentModel>(cancellationToken: cancellationToken);

            string albumsMessage = string.Empty;
            var count = (results?.Feed.Results.Count ?? 0) < 5 ? results.Feed.Results.Count : 5;
            for (var index = 0; index < count; index++)
            {
                var album = results.Feed.Results[index];
                albumsMessage += $"{index + 1}. {album.ArtistName} - {album.Name} \n\r";
            }

            await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                new InlineKeyboardMarkup(results.Feed.Results.Take(5)
                    .Select((x, index) =>
                        InlineKeyboardButton.WithCallbackData((index + 1).ToString(),
                            $"{x.ArtistName} {x.Name}"))), cancellationToken: cancellationToken);
        }

        private async Task SendAudioAsync(long chatId, Stream stream, string title, TimeSpan? videoDuration,
            InputFileUrl thump,
            Author videoAuthor,
            CancellationToken cancellationToken)
        {
            await _botService.Client.SendAudioAsync(chatId, new InputFileStream(stream, title), 
                cancellationToken: cancellationToken,
                duration: (videoDuration.HasValue ? (int?) videoDuration.Value.TotalSeconds : null),
                parseMode: ParseMode.Html, thumbnail:  thump, title: title, disableNotification: true, performer: videoAuthor.ChannelTitle);
            
        }

        public async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
        {
            try
            {
                StreamManifest streamManifest =
                    await _youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

                IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                var stream = await _youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);
                return stream;
            }
            catch
            {
                TrackClient trackClient = new TrackClient();
                Track track = await trackClient.GetTrackInfoAsync(videoId.Value, cancellationToken);
                string requestUri = track.GetStreamWithHighestBitrate()?.Url;
                if (requestUri != null)
                {
                    var streamAsync = await _httpClient.GetStreamAsync(requestUri, cancellationToken);
                    return streamAsync;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
