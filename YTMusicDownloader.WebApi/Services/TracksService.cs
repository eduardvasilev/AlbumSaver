using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YTMusicAPI.Abstraction;
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
            var searchResult = await _trackClient.GetAlbumTracksAsync(albumUrl, cancellationToken);

            var result = new ResultObject<IEnumerable<MusicSearchResult>>(searchResult.Tracks.Select(x => new MusicSearchResult
            {
                Author = x.Author,
                ImageUrl = x.Thumbnails.OrderByDescending(x => x.Resolution?.Width).Skip(1).FirstOrDefault()?.Url,
                RecordType = "track",
                Title = x.Title,
                YouTubeMusicPlaylistUrl = albumUrl,
            }));

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(albumUrl, result, cacheEntryOptions);
        }
     
        return cache;
    }
}