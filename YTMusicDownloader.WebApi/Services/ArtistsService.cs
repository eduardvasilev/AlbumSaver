using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class ArtistsService : IArtistsService
{
    private readonly IArtistClient _artistClient;
    private readonly IMemoryCache _memoryCache;

    public ArtistsService(IArtistClient artistClient, IMemoryCache memoryCache)
    {
        _artistClient = artistClient;
        _memoryCache = memoryCache;
    }


    public async Task<UrlModel> GetArtistImageAsync(string channelUrl, CancellationToken cancellationToken)
    {
        if (!_memoryCache.TryGetValue(channelUrl, out string cache))
        {
            var result = await _artistClient.GetArtistImageAsync(channelUrl, cancellationToken);


            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(channelUrl, result, cacheEntryOptions);
        }

        return new UrlModel(cache);
    }
}