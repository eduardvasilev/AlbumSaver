using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
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
                    await SendSong(chatId, inputText, cancellationToken);
                    return;
                }

                await SendAlbum(inputText, isCallback, message, chatId, cancellationToken);
            }
        }

        private async Task SendAlbum(string inputText, bool isCallback, Message message, long chatId,
            CancellationToken cancellationToken)
        {
            var result = await _youtube.Search.GetPlaylistsAsync(inputText, cancellationToken);
            PlaylistSearchResult playlistSearchResult = result.FirstOrDefault();
            if (playlistSearchResult != null)
            {
                var videos = await _youtube.Playlists.GetVideosAsync(PlaylistId.Parse(playlistSearchResult.Url),
                    cancellationToken);

                if (videos.Count > 20 && isCallback && message == null)
                {
                    await _botService.Client.SendTextMessageAsync(chatId,
                        "You're trying to get more than 20 tracks. Are you sure?", replyMarkup:
                        new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Yes!", inputText)), cancellationToken: cancellationToken);
                    return;
                }

                foreach (PlaylistVideo playlistVideo in videos)
                {
                    await SendSongAsync(chatId, playlistVideo, cancellationToken);
                }
            }
        }

        private async Task SendSong(long chatId, string searchText, CancellationToken cancellationToken)
        {
            Regex regex = new Regex(@"(?<=\/song\s)(.*)");
            string songName = regex.Match(searchText).Value;

            var songs = await _youtube.Search.GetVideosAsync(songName, cancellationToken);

            VideoSearchResult videoSearchResult = songs.FirstOrDefault();
            {
                if (videoSearchResult != null)
                {
                    await SendSongAsync(chatId, videoSearchResult, cancellationToken);
                }
            }
        }

        private async Task SendSongAsync(long chatId, IVideo video, CancellationToken cancellationToken)
        {
            var videoId = VideoId.Parse(video.Id);

            Stream stream = await GetAudioStreamAsync(videoId, cancellationToken);

            await SendAudioAsync(chatId, stream, video.Title, cancellationToken);
        }

        private async Task SendActualAsync(long chatId, CancellationToken cancellationToken)
        {
            HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync(
                "https://rss.applemarketingtools.com/api/v2/us/music/most-played/10/albums.json",
                cancellationToken);

            var results = await httpResponseMessage.Content.ReadFromJsonAsync<ItunesMostRecentModel>(cancellationToken: cancellationToken);

            string albumsMessage = string.Empty;
            for (var index = 0; index < results.Feed.Results.Count; index++)
            {
                var album = results.Feed.Results[index];
                albumsMessage += $"{index + 1}. {album.ArtistName} - {album.Name} \n\r";
            }

            await _botService.Client.SendTextMessageAsync(chatId, albumsMessage, replyMarkup:
                new InlineKeyboardMarkup(results.Feed.Results
                    .Select((x, index) =>
                        InlineKeyboardButton.WithCallbackData((index + 1).ToString(), $"{x.ArtistName} {x.Name}"))), cancellationToken: cancellationToken);
        }

        private async Task SendAudioAsync(long chatId, Stream stream, string title, CancellationToken cancellationToken)
        {
            await _botService.Client.SendAudioAsync(chatId, new InputMedia(stream, title), cancellationToken: cancellationToken);

        }

        private async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
        {
            StreamManifest streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

            IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            return await _youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        }
    }
}
