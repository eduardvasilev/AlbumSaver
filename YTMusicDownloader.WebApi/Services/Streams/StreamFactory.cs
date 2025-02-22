using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YTMusicDownloader.WebApi.Services.Streams.Abstraction;

namespace YTMusicDownloader.WebApi.Services.Streams;

public class StreamFactory
{
    private IOrderedEnumerable<IStreamProvider> _providers;

    public StreamFactory(IServiceProvider serviceProvider)
    {
        _providers = serviceProvider.GetServices<IStreamProvider>().OrderBy(x => x.Order);
    }

    public async Task<Stream> GetStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        foreach (var streamProvider in _providers)
        {
            try
            {
                var stream = await streamProvider.GetStreamAsync(url, cancellationToken);
                if (stream == null)
                {
                    continue;
                }
                return stream;
            }
            catch
            {
                continue;
            }
        }
        
        return await Task.FromResult<Stream>(null);
    }
}