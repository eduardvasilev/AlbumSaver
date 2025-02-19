using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model.Domain;
using YTMusicDownloader.WebApi.Services.Streams.Abstraction;

namespace YTMusicDownloader.WebApi.Services.Streams.Providers;

public class YTMusicProvider : IStreamProvider
{
    private readonly ITrackClient _trackClient;
    private readonly HttpClient _httpClient;
    public YTMusicProvider(ITrackClient trackClient, IHttpClientFactory httpClientFactory)
    {
        _trackClient = trackClient;
        _httpClient = httpClientFactory.CreateClient();
    }

    public StreamProviderType Type  => StreamProviderType.YTMusic;

    public async Task<Stream> GetStreamAsync(string audioUrl, CancellationToken cancellationToken)
    {
        var trackInfo = await _trackClient.GetTrackInfoAsync(audioUrl, cancellationToken);

        var stream = await GetAudioStreamAsync(trackInfo, cancellationToken);
        return stream;
    }
    
    private async Task<Stream> GetAudioStreamAsync(Track track, CancellationToken cancellationToken)
    {
        return await _httpClient.GetStreamAsync(track.Streams.Where(x => x.AudioQuality != null).MaxBy(x => x.Bitrate)?.Url, cancellationToken);
    }

}