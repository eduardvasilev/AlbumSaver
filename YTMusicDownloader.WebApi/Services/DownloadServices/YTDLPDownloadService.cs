using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
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

public class YTDLPDownloadService : IDownloadService
{
    private readonly IBotService _botService;
    private readonly ITrackClient _trackClient;
    private readonly TelemetryClient _telemetryClient;
    private readonly IBackupBackendService _backupBackendService;
    private readonly HttpClient _httpClient;
    private readonly YoutubeClient _youtubeClient;
    private readonly IPaymentService _paymentService;
    //private readonly ITelegramFilesService _telegramFilesService;
    public YTDLPDownloadService(IBotService botService, ITrackClient trackClient, TelemetryClient telemetryClient,
        IHttpClientFactory httpClientFactory, IBackupBackendService backupBackendService, YoutubeClient youtubeClient, IPaymentService paymentService)
    {
        _botService = botService;
        _trackClient = trackClient;
        _telemetryClient = telemetryClient;
        _backupBackendService = backupBackendService;
        _paymentService = paymentService;
        _youtubeClient = new YoutubeClient();
        _httpClient = httpClientFactory.CreateClient();
        //_telegramFilesService = telegramFilesService;
    }

    public async Task SendAlbumAsync(DownloadRequest request, CancellationToken cancellationToken)
    {

        AlbumTracksResult result = await _trackClient.GetAlbumTracksAsync(request.YouTubeMusicPlaylistUrl, CancellationToken.None);


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
                await TrySendWithYTDLP(request.UserId, track, new InputFileUrl(track.Thumbnails.MinBy(x => x.Resolution.Width).Url), cancellationToken);
            }

            await _paymentService.SendDonateMessageAsync(request.UserId);
        }
    }

    public async Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken)
    {
        if (!await TrySendTrackYtDlp(model.UserId, model.YouTubeMusicPlaylistUrl))
        {
            //  await _backupBackendService.TrySendMusicAsync(model.UserId, model.YouTubeMusicPlaylistUrl, Model.EntityType.Track);
        }
    }

    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(requestUrl), cancellationToken);

            await SendTrackAsync(request.UserId, result, cancellationToken);
        }

        await _paymentService.SendDonateMessageAsync(request.UserId);
    }

    public async Task SendTrackAsync(long chatId, Video track, CancellationToken cancellationToken)
    {
        InputFileUrl thumbnail = new InputFileUrl((track.Thumbnails.ToList().MinBy(x => x.Resolution.Area)?.Url) ?? track.Thumbnails.ToList().FirstOrDefault().Url);

        try
        {
            await SendSongInternalAsync(chatId, track, thumbnail, cancellationToken);
        }

        catch (Exception exception)
        {

            var song = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);
            if (!await TrySendWithYTDLP(chatId, song, thumbnail, cancellationToken))
            {
                _telemetryClient.TrackException(exception);

                if (!await _backupBackendService.TrySendMusicAsync(chatId, track.Url, Model.EntityType.Track))
                {
                    //await _botService.Client.SendTextMessageAsync(chatId,
                    //    $"Sorry, we couldn't send the track: {track.Title}. Our service may be blocked. But we will definitely be back");
                }
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

        //string fileId = await _telegramFilesService.GetFileIdAsync(track.Url);

        //if (fileId != null)
        //{
        //    await _botService.Client.SendAudioAsync(chatId, InputFile.FromFileId(fileId),
        //            cancellationToken: CancellationToken.None,
        //            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
        //            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true, performer: track.Author.ChannelTitle);

        //    return;
        //}

        await using Stream stream = await GetAudioStreamAsync(track.Id, cancellationToken);

        var sendAudioAsync = await _botService.Client.SendAudioAsync(chatId, new InputFileStream(stream, track.Title + ".mp3"),
            cancellationToken: CancellationToken.None,
            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
            performer: author, protectContent: false);

        //await _telegramFilesService.SetFileIdAsync(track.Url, sendAudioAsync.Audio.FileId);
    }

    private async Task<bool> TrySendWithYTDLP(long chatId, Track track, InputFileUrl thump, CancellationToken cancellationToken)
    {
        try
        {
            var memoryStream = await DownloadAudioAsync(track.Url);


            var sendAudioAsync = await _botService.Client.SendAudioAsync(chatId, new InputFileStream(memoryStream, track.Title + ".mp3"),
                cancellationToken: CancellationToken.None,
                duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
                parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
                performer: track.Author,
                protectContent: false);


            return true;
        }

        catch
        {
            return false;
        }

    }

    public async Task<Stream> GetAudioStreamAsync(VideoId videoId, CancellationToken cancellationToken)
    {
        StreamManifest streamManifest =
            await _youtubeClient.Videos.Streams.GetManifestAsync(videoId, cancellationToken);

        IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        return stream;
    }

    private async Task<bool> TrySendAlbumWithYTLDP(long chatId, string albumUrl, CancellationToken cancellation)
    {
        try
        {
            var tracks = await GetPlaylistVideoUrlsAsync(albumUrl);

            foreach (var track in tracks)
            {
                await TrySendTrackYtDlp(chatId, track);
            }
            return true;
        }
        catch (Exception ex)
        {
            {

                await _botService.Client.SendTextMessageAsync(chatId, ex.Message);
                return false;
            }
        }

    }

    private async Task<bool> TrySendTrackYtDlp(long chatId, string track)
    {
        try
        {
            var metadata = await GetVideoMetadataAsync(track);

            using (var memoryStream = await DownloadAudioAsync(track))
            {

                var sendAudioAsync = await _botService.Client.SendAudioAsync(chatId,
                    new InputFileStream(memoryStream, metadata["title"].ToString() + ".mp3"),
                    cancellationToken: CancellationToken.None,
                    duration: (int.Parse(metadata["duration"].ToString())),
                    parseMode: ParseMode.Html, thumbnail: new InputFileUrl(metadata["thumbnail"].ToString()),
                    title: metadata["title"].ToString(), disableNotification: true,
                    performer: metadata["artist"].ToString());
                return true;
            }
        }
        catch (Exception ex)
        {
            await _botService.Client.SendTextMessageAsync(chatId, ex.Message);
            return false;
        }
    }

    private async Task<List<string>> GetPlaylistVideoUrlsAsync(string playlistUrl)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments =
                $"--flat-playlist -J --no-warnings \"{playlistUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);

        if (process == null)
            return null;

        string json = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"yt-dlp docker failed: {error}");

        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries");

        var urls = new List<string>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("url", out var id))
                urls.Add(id.GetString());
        }

        return urls;
    }


    private async Task<MemoryStream?> DownloadAudioAsync(string videoUrl)
    {

        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments =
                $"--extract-audio --audio-format mp3 " +
                $"--buffer-size 16K " +
                $"-o - --quiet --no-warnings --no-playlist \"{videoUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = psi };

        process.Start();

        var stream = new MemoryStream(); // ← БЕЗ using

        await process.StandardOutput.BaseStream.CopyToAsync(stream);
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            stream.Dispose();
            throw new Exception($"yt-dlp docker failed: {error}");
        }

        stream.Position = 0;
        return stream.Length > 0 ? stream : null;
    }



    public async Task<Dictionary<string, object>?> GetVideoMetadataAsync(string url)
    {

        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments =
                $"-j --no-warnings  \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null)
            return null;

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"yt-dlp docker failed: {error}");

        return JsonSerializer.Deserialize<Dictionary<string, object>>(output);
    }


}
