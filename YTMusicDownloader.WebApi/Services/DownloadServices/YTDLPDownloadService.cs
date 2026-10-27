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
using YoutubeExplode.Common;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicAPI.Model.Domain;
using YTMusicDownloader.WebApi.Model;
using Video = YoutubeExplode.Videos.Video;

namespace YTMusicDownloader.WebApi.Services;

public class YTDLPDownloadService(IBotService botService, ITrackClient trackClient,
    IPaymentService paymentService) : IDownloadService
{
    private readonly IBotService _botService = botService;
    private readonly ITrackClient _trackClient = trackClient;
    private readonly IPaymentService _paymentService = paymentService;

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
        await TrySendTrackYtDlp(model.UserId, model.YouTubeMusicPlaylistUrl);
    }

    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            var track = await  _trackClient.GetTrackInfoAsync(requestUrl, cancellationToken);
            await TrySendWithYTDLP(request.UserId, track, new InputFileUrl(track.Thumbnails.MinBy(x => x.Resolution.Width).Url), cancellationToken);

        }

        await _paymentService.SendDonateMessageAsync(request.UserId);
    }

    public async Task SendTrackAsync(long chatId, Video track, CancellationToken cancellationToken)
    {
        InputFileUrl thumbnail = new InputFileUrl((track.Thumbnails.ToList().MinBy(x => x.Resolution.Area)?.Url) ?? track.Thumbnails.ToList().FirstOrDefault().Url);
        var song = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);
        await TrySendWithYTDLP(chatId, song, thumbnail, cancellationToken);
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


    private async Task<MemoryStream?> DownloadAudioAsync(
     string videoUrl,
     CancellationToken cancellationToken = default)
    {
        var psi = CreateYtDlpStartInfo(
            "--extract-audio",
            "--audio-format", "mp3",
            "--buffer-size", "16K",
            "-o", "-",
            "--quiet",
            "--no-warnings",
            "--no-playlist",
            videoUrl
        );

        using var process = new Process { StartInfo = psi };

        process.Start();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
        });

        using var stream = new MemoryStream();

        // Читаем stdout и stderr параллельно, чтобы избежать deadlock.
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(
            stream, cancellationToken);

        var errorTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        try
        {
            await Task.WhenAll(
                copyTask,
                errorTask,
                process.WaitForExitAsync(cancellationToken));

            if (process.ExitCode != 0)
            {
                throw new Exception(
                    $"yt-dlp failed (exit code {process.ExitCode}): " +
                    await errorTask);
            }

            if (stream.Length == 0)
                return null;

            stream.Position = 0;

            // Возвращаем поток, который должен освободить вызывающий код.
            return new MemoryStream(stream.ToArray());
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }

            throw;
        }
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

        psi.ArgumentList.Add("--proxy");
        psi.ArgumentList.Add("socks5://tor:9050");

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

    private static ProcessStartInfo CreateYtDlpStartInfo(
    params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--proxy");
        psi.ArgumentList.Add("socks5://tor:9050");

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        return psi;
    }

}
