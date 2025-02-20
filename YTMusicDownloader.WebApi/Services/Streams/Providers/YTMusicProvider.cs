using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Downloader;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model.Domain;
using YTMusicDownloader.WebApi.Services.Streams.Abstraction;

namespace YTMusicDownloader.WebApi.Services.Streams.Providers;

public class YTMusicProvider : IStreamProvider
{
    private readonly ITrackClient _trackClient;
    private readonly HttpClient _httpClient;
    private DownloadService _downloader;

    public YTMusicProvider(ITrackClient trackClient, IHttpClientFactory httpClientFactory)
    {
        _trackClient = trackClient;
        _httpClient = httpClientFactory.CreateClient();
        
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8, // Number of file parts, default is 1
            ParallelDownload = true // Download parts in parallel (default is false)
        };
        
        _downloader = new DownloadService(downloadOpt);
        
    }

    public StreamProviderType Type  => StreamProviderType.YTMusic;

    public async Task<Stream> GetStreamAsync(string audioUrl, CancellationToken cancellationToken)
    {
        var trackInfo = await _trackClient.GetTrackInfoAsync(audioUrl, cancellationToken);

        var audio = trackInfo.Streams.Where(x => x.AudioQuality != null).MaxBy(x => x.Bitrate);
        var stream = await _downloader.DownloadFileTaskAsync(audio.Url, cancellationToken);
        return stream;
    }
}