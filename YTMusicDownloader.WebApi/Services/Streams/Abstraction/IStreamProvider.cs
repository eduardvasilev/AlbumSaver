using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YTMusicDownloader.WebApi.Services.Streams.Abstraction;

public interface IStreamProvider
{
    public StreamProviderType Type { get; }
    public int Order => (int)Type;

    Task<Stream> GetStreamAsync(string audioUrl, CancellationToken cancellationToken = default);
}