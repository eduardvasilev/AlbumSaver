using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicAPI.Model.Domain;
using YTMusicAPI.Utils;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services.Telegram;
using EntityType = YTMusicAPI.Model.EntityType;
using Video = YoutubeExplode.Videos.Video;

namespace YTMusicDownloader.WebApi.Services;

public class DownloadService2 : IDownloadService
{
    private readonly IBotService _botService;
    private readonly ITrackClient _trackClient;
    private readonly TelemetryClient _telemetryClient;
    private readonly IBackupBackendService _backupBackendService;
    private readonly HttpClient _httpClient;
    private readonly YoutubeClient _youtubeClient;
    private readonly ITelegramFilesService _telegramFilesService;
    public DownloadService2(IBotService botService, ITrackClient trackClient, TelemetryClient telemetryClient,
        IHttpClientFactory httpClientFactory, IBackupBackendService backupBackendService, YoutubeClient youtubeClient, ITelegramFilesService telegramFilesService)
    {
        _botService = botService;
        _trackClient = trackClient;
        _telemetryClient = telemetryClient;
        _backupBackendService = backupBackendService;
        _youtubeClient = new YoutubeClient();
        _httpClient = httpClientFactory.CreateClient();
        _telegramFilesService = telegramFilesService;
    }

    public async Task SendAlbumAsync(DownloadRequest request, CancellationToken cancellationToken)
    {
        AlbumTracksResult result;
        try
        {
            result = await _trackClient.GetAlbumTracksAsync(request.YouTubeMusicPlaylistUrl, cancellationToken);
        }
        catch (Exception exception)
        {
            if (!await _backupBackendService.TrySendMusicAsync(request.UserId, request.YouTubeMusicPlaylistUrl, Model.EntityType.Album))
            {
                throw exception;
            }
            return;
        }

        if (result != null && result.Tracks.Any())
        {
            string cover = result.Thumbnails.Skip(1).FirstOrDefault()?.Url;
            if (!string.IsNullOrWhiteSpace(cover))
            {
                InputFileUrl inputOnlineFile = new InputFileUrl(cover);

                await _botService.Client.SendPhotoAsync(request.UserId,
                    inputOnlineFile, caption: $"{result.AlbumTitle}", cancellationToken: cancellationToken);
            }

            foreach (var track in result.Tracks)
            {
                var trackInfo = await _youtubeClient.Videos.GetAsync(VideoId.Parse(track.Url), cancellationToken);

                await SendTrackAsync(request.UserId, trackInfo, cancellationToken);
            }

            await SendDonateMessage(request.UserId);
        }
    }

    public async Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken)
    {
        var track = await _youtubeClient.Videos.GetAsync(VideoId.Parse(model.YouTubeMusicPlaylistUrl), cancellationToken);
        await SendTrackAsync(model.UserId, track, cancellationToken);
        await SendDonateMessage(model.UserId);
    }

    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(requestUrl), cancellationToken);

            await SendTrackAsync(request.UserId, result, cancellationToken);
        }

        await SendDonateMessage(request.UserId);
    }

    public async Task SendTrackAsync(long chatId, Video track, CancellationToken cancellationToken)
    {
        try
        {
            InputFileUrl thumbnail = new InputFileUrl(track.Thumbnails.ToList().FirstOrDefault()?.Url);

            await SendSongInternalAsync(chatId, track, thumbnail, cancellationToken);
        }

        catch (Exception exception)
        {
            _telemetryClient.TrackException(exception);

            if (!await _backupBackendService.TrySendMusicAsync(chatId, track.Url, Model.EntityType.Track))
            {
                await _botService.Client.SendTextMessageAsync(chatId,
                    $"Sorry, we couldn't send the track: {track.Title}. Our service may be blocked. But we will definitely be back");
            }
        }
    }

    private async Task SendSongInternalAsync(long chatId, YoutubeExplode.Videos.Video track, InputFileUrl thump, CancellationToken cancellationToken)
    {
        string author = track.Author.ChannelTitle;
        var topic = " - Topic";
        if (author != null && author.EndsWith(topic))
        {
            author = author.Substring(0, author.Length - topic.Length);
        }

        await using Stream stream = await GetAudioStreamAsync(track.Id, cancellationToken);

        var sendAudioAsync = await _botService.Client.SendAudio(chatId, new InputFileStream(stream, track.Title),
            cancellationToken: CancellationToken.None,
            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
            performer: author, protectContent: false);

    }

    public async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
    {
        StreamManifest streamManifest =
            await _youtubeClient.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

        IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        return stream;
    }

    private async Task SendDonateMessage(long userId)
    {
        DownloadSetRequest request;
        await _botService.Client.SendInvoice(userId, "Buy us a coffee",
            "Support us so we can add new features. Use /feedback command for your suggestions",
            Guid.NewGuid().ToString(), "XTR", new List<LabeledPrice>() { new("Donate us", 1) });
    }
}