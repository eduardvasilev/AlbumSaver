using Microsoft.ApplicationInsights;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model.Domain;
using YTMusicAPI.Utils;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class DownloadService : IDownloadService
{
    private readonly IBotService _botService;
    private readonly ITrackClient _trackClient;
    private readonly TelemetryClient _telemetryClient;
    private readonly HttpClient _httpClient;

    public DownloadService(IBotService botService, ITrackClient trackClient, TelemetryClient telemetryClient, IHttpClientFactory httpClientFactory)
    {
        _botService = botService;
        _trackClient = trackClient;
        _telemetryClient = telemetryClient;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task SendAlbumAsync(DownloadRequest request, CancellationToken cancellationToken)
    {
        var result = await _trackClient.GetAlbumTracksAsync(request.YouTubeMusicPlaylistUrl, cancellationToken);

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
                var trackInfo = await _trackClient.GetTrackInfoAsync(track.Url, cancellationToken);

                await SendTrackAsync(request.UserId, trackInfo, cancellationToken);
            }
        }
    }

    public async Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken)
    {
        Track track = await _trackClient.GetTrackInfoAsync(model.YouTubeMusicPlaylistUrl, cancellationToken);
        await SendTrackAsync(model.UserId, track, cancellationToken);
    }

    public async Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken)
    {
        foreach (var requestUrl in request.Urls)
        {
            var result = await _trackClient.GetTrackInfoAsync(requestUrl, cancellationToken);

            await SendTrackAsync(request.UserId, result, cancellationToken);
        }
    }

    public async Task SendTrackAsync(long chatId, Track track, CancellationToken cancellationToken)
    {
        try
        {
            InputFileUrl thumbnail = new InputFileUrl(track.Thumbnails.ToList().GetLowest()?.Url);

            await SendSongInternalAsync(chatId, track, thumbnail, cancellationToken);
        }

        catch (Exception exception)
        {
            _telemetryClient.TrackException(exception);

            await _botService.Client.SendTextMessageAsync(chatId,
                $"Sorry, we couldn't send the track: {track.Title}. Please try again.");
        }
    }

    private async Task SendSongInternalAsync(long chatId, Track track, InputFileUrl thump, CancellationToken cancellationToken)
    {
        await using Stream stream = await GetAudioStreamAsync(track, CancellationToken.None);
        await _botService.Client.SendAudioAsync(chatId, new InputFileStream(stream, track.Title),
            cancellationToken: CancellationToken.None,
            duration: (track.Duration.HasValue ? (int?)track.Duration.Value.TotalSeconds : null),
            parseMode: ParseMode.Html, thumbnail: thump, title: track.Title, disableNotification: true,
            performer: track.Author);
    }

    private async Task<Stream> GetAudioStreamAsync(Track track, CancellationToken cancellationToken)
    {
        return await _httpClient.GetStreamAsync(track.Streams.LastOrDefault()?.Url, cancellationToken);
    }
}