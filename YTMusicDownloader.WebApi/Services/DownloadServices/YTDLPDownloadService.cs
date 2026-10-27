using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;

using YoutubeExplode.Common;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicAPI.Model.Domain;
using YTMusicDownloader.WebApi.Model;
using YTMusicDownloader.WebApi.Services.Telegram;

using Video = YoutubeExplode.Videos.Video;

namespace YTMusicDownloader.WebApi.Services;

public class YTDLPDownloadService(IBotService botService, ITrackClient trackClient,
    IPaymentService paymentService, ITelegramFilesService telegramFilesService, ILogger<YTDLPDownloadService> logger) : IDownloadService

{
    private readonly IBotService _botService = botService;
    private readonly ITrackClient _trackClient = trackClient;
    private readonly IPaymentService _paymentService = paymentService;
    private readonly ITelegramFilesService _telegramFilesService = telegramFilesService;
    private readonly ILogger<YTDLPDownloadService> _logger = logger;



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
                var thumbnail = new InputFileUrl(track.Thumbnails.MinBy(x => x.Resolution.Width).Url);
                if (!await TrySendWithYTDLP(request.UserId, track, thumbnail, cancellationToken))
                    _logger.LogWarning("Failed to send album track {TrackUrl} to chat {ChatId}.", track.Url, request.UserId);

            }


            await _paymentService.SendDonateMessageAsync(request.UserId);
        }
    }

    public async Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken)
    {
        if (!await TrySendTrackYtDlp(model.UserId, model.YouTubeMusicPlaylistUrl, cancellationToken))
        {
            _logger.LogWarning("Track request failed for {TrackUrl} in chat {ChatId}.",
                model.YouTubeMusicPlaylistUrl, model.UserId);
        }


    }



    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            var fileId = await GetCachedFileIdAsync(requestUrl);
            if (!string.IsNullOrEmpty(fileId) &&
                await TrySendCachedAudioAsync(request.UserId, requestUrl, fileId, cancellationToken))
            {
                continue;
            }

            var track = await _trackClient.GetTrackInfoAsync(requestUrl, cancellationToken);

            var thumbnail = new InputFileUrl(track.Thumbnails.MinBy(x => x.Resolution.Width).Url);
            if (!await TrySendWithYTDLP(request.UserId, track, thumbnail, cancellationToken))
                _logger.LogWarning("Failed to send track {TrackUrl} to chat {ChatId}.", track.Url, request.UserId);



        }


        await _paymentService.SendDonateMessageAsync(request.UserId);
    }

    public async Task SendTrackAsync(long chatId, Video track, CancellationToken cancellationToken)
    {
        InputFileUrl thumbnail = new InputFileUrl((track.Thumbnails.ToList().MinBy(x => x.Resolution.Area)?.Url) ?? track.Thumbnails.ToList().FirstOrDefault().Url);
        var song = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);
        if (!await TrySendWithYTDLP(chatId, song, thumbnail, cancellationToken))
            _logger.LogWarning("Failed to send track {TrackUrl} to chat {ChatId}.", track.Url, chatId);


    }


    private async Task<string> GetCachedFileIdAsync(string trackUrl)

    {
        try
        {
            return await _telegramFilesService.GetFileIdAsync(trackUrl);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to read cached Telegram file ID for {TrackUrl}; downloading track instead.", trackUrl);
            return null;
        }
    }

    private async Task<bool> TrySendCachedAudioAsync(long chatId, string trackUrl, string fileId,
        CancellationToken cancellationToken, int? duration = null, string title = null, string performer = null)
    {
        try
        {
            await _botService.Client.SendAudioAsync(chatId, InputFile.FromFileId(fileId),
                cancellationToken: CancellationToken.None,

                duration: duration,
                title: title,
                performer: performer,
                disableNotification: true);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cached Telegram file ID is unusable for {TrackUrl}; sending track from download.", trackUrl);
            return false;
        }
    }

    private async Task SaveFileIdAsync(string trackUrl, string fileId)

    {
        try
        {
            await _telegramFilesService.SetFileIdAsync(trackUrl, fileId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to save Telegram file ID for {TrackUrl}.", trackUrl);
        }
    }


    private async Task<bool> TrySendWithYTDLP(long chatId, Track track, InputFileUrl thump, CancellationToken cancellationToken)

    {
        var fileId = await GetCachedFileIdAsync(track.Url);

        try
        {
            if (!string.IsNullOrEmpty(fileId) &&
                await TrySendCachedAudioAsync(chatId, track.Url, fileId, cancellationToken,
                    track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null,
                    track.Title, track.Author))
            {
                return true;
            }


            using var memoryStream = await DownloadAudioAsync(track.Url);
            if (memoryStream == null)
                return false;

            var sentAudio = await _botService.Client.SendAudioAsync(chatId, new InputFileStream(memoryStream, track.Title + ".mp3"),
                cancellationToken: CancellationToken.None,

                duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
                parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
                performer: track.Author,
                protectContent: false);

            await SaveFileIdAsync(track.Url, sentAudio.Audio.FileId);
            return true;

        }

        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send track {TrackUrl} to chat {ChatId}.", track.Url, chatId);
            return false;

        }

    }

    private async Task<bool> TrySendTrackYtDlp(long chatId, string track, CancellationToken cancellationToken)
    {
        var fileId = await GetCachedFileIdAsync(track);
        try
        {
            if (!string.IsNullOrEmpty(fileId) &&
                await TrySendCachedAudioAsync(chatId, track, fileId, cancellationToken))
            {
                return true;
            }

            var metadata = await GetVideoMetadataAsync(track);
            if (metadata == null)
                return false;

            using var memoryStream = await DownloadAudioAsync(track);

            if (memoryStream == null)
                return false;

            var sentAudio = await _botService.Client.SendAudioAsync(chatId,
                new InputFileStream(memoryStream, metadata["title"].ToString() + ".mp3"),
                cancellationToken: CancellationToken.None,
                duration: int.Parse(metadata["duration"].ToString()),

                parseMode: ParseMode.Html, thumbnail: new InputFileUrl(metadata["thumbnail"].ToString()),
                title: metadata["title"].ToString(), disableNotification: true,
                performer: metadata["artist"].ToString());
            await SaveFileIdAsync(track, sentAudio.Audio.FileId);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to send track {TrackUrl} to chat {ChatId}.", track, chatId);
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

        var errorTask = process.StandardError.ReadToEndAsync();
        string json = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();
        string error = await errorTask;


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

        var stream = new MemoryStream();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.StandardOutput.BaseStream.CopyToAsync(stream);
        await process.WaitForExitAsync();
        string error = await errorTask;


        if (process.ExitCode != 0)
        {
            stream.Dispose();
            throw new Exception($"yt-dlp docker failed: {error}");
        }

        stream.Position = 0;
        return stream.Length > 0 ? stream : null;
    }

    /// <summary>
    /// Gets metadata for the specified video URL by invoking the external yt-dlp process,
    /// reading its JSON output and deserializing it into a dictionary.
    /// </summary>
    /// <param name="url">The video or playlist URL to query with yt-dlp.</param>
    /// <returns>
    /// A dictionary of metadata keys to values parsed from yt-dlp's JSON output.
    /// Returns <c>null</c> when the yt-dlp process could not be started.
    /// </returns>
    /// <exception cref="Exception">Thrown when yt-dlp exits with a non-zero code.</exception>
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

        var errorTask = process.StandardError.ReadToEndAsync();
        string output = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();
        string error = await errorTask;


        if (process.ExitCode != 0)
            throw new Exception($"yt-dlp docker failed: {error}");

        return JsonSerializer.Deserialize<Dictionary<string, object>>(output);
    }


}
