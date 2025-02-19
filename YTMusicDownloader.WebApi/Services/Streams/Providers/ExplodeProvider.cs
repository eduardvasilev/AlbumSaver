using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YTMusicDownloader.WebApi.Services.Streams.Abstraction;

namespace YTMusicDownloader.WebApi.Services.Streams.Providers;

public class ExplodeProvider : IStreamProvider
{
    private readonly YoutubeClient _youtubeClient;
    public ExplodeProvider()
    {
        _youtubeClient = new YoutubeClient();
    }

    public StreamProviderType Type => StreamProviderType.Explode;

    public async Task<Stream> GetStreamAsync(string audioUrl, CancellationToken cancellationToken)
    {
        StreamManifest streamManifest =
            await _youtubeClient.Videos.Streams.GetManifestAsync(audioUrl, cancellationToken);

        IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo, cancellationToken);
        return stream;
    }
}