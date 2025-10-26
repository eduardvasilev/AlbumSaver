using AngleSharp.Io;
using Microsoft.ApplicationInsights;
using RTools_NTS.Util;
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
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YTMusicAPI;
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
    private readonly IPaymentService _paymentService;
    private readonly StreamDownloader _streamDownloader;
    //private readonly ITelegramFilesService _telegramFilesService;
    public DownloadService2(IBotService botService, ITrackClient trackClient, TelemetryClient telemetryClient,
        IHttpClientFactory httpClientFactory, IBackupBackendService backupBackendService, YoutubeClient youtubeClient, 
        IPaymentService paymentService, StreamDownloader streamDownloader)
    {
        //var cookies = CookieParser.LoadCookiesFromTxt("./cookies.txt");
        _botService = botService;
        _trackClient = trackClient;
        _telemetryClient = telemetryClient;
        _backupBackendService = backupBackendService;
        _paymentService = paymentService;
        _youtubeClient = new YoutubeClient();
        _httpClient = httpClientFactory.CreateClient();
        _streamDownloader = streamDownloader;
    }

    public async Task SendAlbumAsync(DownloadRequest request, CancellationToken cancellationToken)
    {
        AlbumTracksResult result;
        try
        {
            result = await _trackClient.GetAlbumTracksAsync(request.YouTubeMusicPlaylistUrl, default);
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
                     inputOnlineFile, caption: result.AlbumTitle);
            }

            foreach (var track in result.Tracks)
            {
                try
                {

                    var trackInfo  = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);

                    await SendTrackAsync(request.UserId, trackInfo, default);
                }
                catch(VideoUnavailableException videoUnavailableException )
                {
                    await _backupBackendService.TrySendMusicAsync(request.UserId, track.Url, Model.EntityType.Track);
                }
            }

            await _paymentService.SendDonateMessageAsync(request.UserId);
        }
    }

    public async Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken)
    {
        Track track;
        try
        {

            track = await _trackClient.GetTrackInfoAsync(VideoId.Parse(model.YouTubeMusicPlaylistUrl), default);

        }
        catch
        {
            await _backupBackendService.TrySendMusicAsync(model.UserId, model.YouTubeMusicPlaylistUrl, Model.EntityType.Track);
            await _paymentService.SendDonateMessageAsync(model.UserId);
            return;

        }
        await SendTrackAsync(model.UserId, track, cancellationToken);
        await _paymentService.SendDonateMessageAsync(model.UserId);
    }

    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            Track result = await _trackClient.GetTrackInfoAsync(VideoId.Parse(requestUrl), default);

            await SendTrackAsync(request.UserId, result, cancellationToken);
        }

        await _paymentService.SendDonateMessageAsync(request.UserId);
    }

    public async Task SendTrackAsync(long chatId, Track track, CancellationToken cancellationToken)
    {
        try
        {
            InputFileUrl thumbnail = new InputFileUrl((track.Thumbnails.ToList().MinBy(x => x.Resolution.Width)?.Url) ?? track.Thumbnails.ToList().FirstOrDefault().Url);

            await SendSongInternalAsync(chatId, track, thumbnail, cancellationToken);
        }

        catch (Exception exception)
        {
            _telemetryClient.TrackException(exception);


            if (!await _backupBackendService.TrySendMusicAsync(chatId, track.Url, Model.EntityType.Track))
            {
                //await _botService.Client.SendTextMessageAsync(chatId,
                //    $"Sorry, we couldn't send the track: {track.Title}. Our service may be blocked. But we will definitely be back");
            }
        }
    }

    private async Task SendSongInternalAsync(long chatId, Track track, InputFileUrl thump, CancellationToken cancellationToken)
    {
        string author = track.Author;
        var topic = " - Topic";
        if (author != null && author.EndsWith(topic))
        {
            author = author.Substring(0, author.Length - topic.Length);
        }

        //string fileId = await _telegramFilesService.GetFileIdAsync(track.Url);

        //if (fileId != null)
        //{
        //    await _botService.Client.SendAudioAsync(chatId, InputFile.FromFileId(fileId),
        //            cancellationToken: CancellationToken.None,
        //            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
        //            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true, performer: track.Author.ChannelTitle);

        //    return;
        //}

        var trackInfo = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);

        var streamInfo = trackInfo.Streams.MaxBy(x => x.AudioQuality);

        //await using MemoryStream stream = await DownloadStreamToMemoryAsync(streamInfo.Url);

        //await using Stream stream = await GetAudioStreamAsync(track.Id, cancellationToken);


        var progress = new Progress<double>(p => Console.WriteLine($"Progress: {p:P1}"));

        var memoryStream = await _streamDownloader.DownloadInChunksAsync(
            streamInfo.Url,
            chunkSize: 1_000_000,
            progress: progress,
            cancellationToken: CancellationToken.None);



        var sendAudioAsync = await _botService.Client.SendAudioAsync(chatId, new InputFileStream(memoryStream, track.Title + ".mp3"),
            cancellationToken: CancellationToken.None,
            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
            performer: author, protectContent: false);

        //await _telegramFilesService.SetFileIdAsync(track.Url, sendAudioAsync.Audio.FileId);
    }

    public async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
    {
        StreamManifest streamManifest =
            await new YoutubeClient().Videos.Streams.GetManifestAsync(videoId);

        IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        return stream;
    }


    public async Task<MemoryStream> DownloadStreamToMemoryAsync(string url)
    {
        using var http = new HttpClient();
        var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }
}