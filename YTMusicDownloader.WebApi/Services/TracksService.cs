using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class TracksService : ITracksService
{
    private readonly ITrackClient _trackClient;
    private readonly IMemoryCache _memoryCache;

    public TracksService(ITrackClient trackClient, IMemoryCache memoryCache)
    {
        _trackClient = trackClient;
        _memoryCache = memoryCache;
    }
    public async Task<ResultObject<IEnumerable<MusicSearchResult>>> GetAlbumTracksAsync(string albumUrl, CancellationToken cancellationToken)
    {
        if (!_memoryCache.TryGetValue(albumUrl, out ResultObject<IEnumerable<MusicSearchResult>> cache))
        {
            var searchResult = await _trackClient.GetAlbumTracks(albumUrl, cancellationToken);

            var result = new ResultObject<IEnumerable<MusicSearchResult>>(searchResult.Select(x => new MusicSearchResult
            {
                Author = x.Author,
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                RecordType = "track",
                Title = x.Title,
                YouTubeMusicPlaylistUrl = albumUrl
            }));

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(albumUrl, result, cacheEntryOptions);
        }
     
        return cache;
    }
}