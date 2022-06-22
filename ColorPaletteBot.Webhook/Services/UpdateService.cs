using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YTMusicDownloader.WebApi.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly IBotService _botService;

        public UpdateService(IBotService botService)
        {
            _botService = botService;
        }

        public async Task ProcessAsync(Update update, CancellationToken cancellationToken)
        {
            var message = update.Message;

            if (message?.Type == MessageType.Text)
            {
                if (update.Message != null)
                {
                    var chatId = update.Message.Chat.Id;

                    var messageText = update.Message.Text;

                    if (messageText == "/start" || string.IsNullOrWhiteSpace(messageText))
                    {
                        return;
                    }
                    var youtube = new YoutubeClient();


                    if (messageText.StartsWith("/song"))
                    {
                        Regex regex = new Regex(@"(?<=\/song\s)(.*)");
                        string songName = regex.Match(messageText).Value;

                        var songs = await youtube.Search.GetVideosAsync(songName, cancellationToken);

                        VideoSearchResult videoSearchResult = songs.FirstOrDefault();
                        {
                            if (videoSearchResult != null)
                            {
                                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(VideoId.Parse(videoSearchResult.Id), cancellationToken);

                                IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                                Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);

                                await _botService.Client.SendAudioAsync(chatId,
                                    new InputMedia(stream, videoSearchResult.Title), cancellationToken: cancellationToken);

                            }
                        }
                        return;
                    }


                    var result = await youtube.Search.GetPlaylistsAsync(messageText, cancellationToken);
                    PlaylistSearchResult playlistSearchResult = result.FirstOrDefault();
                    if (playlistSearchResult != null)
                    {
                        var videos = await youtube.Playlists.GetVideosAsync(PlaylistId.Parse(playlistSearchResult.Url), cancellationToken);
                        foreach (var playlistVideo in videos)
                        {
                            VideoId videoId = VideoId.Parse(playlistVideo.Url);
                            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

                            IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo, cancellationToken);

                            await _botService.Client.SendAudioAsync(chatId, new InputMedia(stream, playlistVideo.Title),  cancellationToken: cancellationToken);
                        }
                    }
                }
            }
        }
    }
}
