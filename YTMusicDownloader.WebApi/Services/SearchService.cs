using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YTMusicAPI;
using YTMusicAPI.Abstraction;
using YTMusicAPI.Model;
using YTMusicAPI.Model.Infrastructure;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class SearchService : ISearchService
{
    private readonly ISearchClient _searchClient;
    private readonly IReleasesClient _releasesClient;
    private readonly IMemoryCache _memoryCache;

    public SearchService(ISearchClient searchClient, IReleasesClient releasesClient, IMemoryCache memoryCache)
    {
        _searchClient = searchClient;
        _releasesClient = releasesClient;
        _memoryCache = memoryCache;
    }

    public async Task<PagingResult<MusicSearchResult>> SearchAlbumsAsync(QueryRequest queryRequest, CancellationToken cancellationToken)
    {
        if (queryRequest.ContinuationNeed == true || !_memoryCache.TryGetValue(queryRequest.Query + queryRequest.ContinuationData?.Token, out PagingResult<MusicSearchResult> cache))
        {
            var searchResult = await _searchClient.SearchAlbumsAsync(queryRequest, cancellationToken);

            PagingResult<MusicSearchResult> result = new PagingResult<MusicSearchResult>(searchResult.Result.Select(x => new MusicSearchResult
            {
                RecordType = "album",
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                Author = x.Author,
                Title = x.Title,
                Year = int.TryParse(x.Year, out int year) ? year : null,
                YouTubeMusicPlaylistUrl = x.Url
            }))
            {
                ContinuationToken = searchResult.ContinuationToken,
                Token = searchResult.Token
            };

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(queryRequest.Query + queryRequest.ContinuationData?.Token, result, cacheEntryOptions);
        }
  
        return cache;
    }

    public async Task<PagingResult<MusicSearchResult>> SearchTracksAsync(QueryRequest queryRequest, CancellationToken cancellationToken)
    {
        if (queryRequest.ContinuationNeed == true || !_memoryCache.TryGetValue(queryRequest.Query+queryRequest.ContinuationData?.Token, out PagingResult<MusicSearchResult> cache))
        {
            var searchResult = (await _searchClient.SearchTracksAsync(queryRequest, cancellationToken));
            var result = new PagingResult<MusicSearchResult>(searchResult.Result.Select(x => new MusicSearchResult
            {
                RecordType = "track",
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                Author = x.Author,
                Title = x.Title,
                YouTubeMusicPlaylistUrl = x.Url

            }))
            {
                ContinuationToken = searchResult.ContinuationToken,
                Token = searchResult.Token
            };

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(queryRequest.Query + queryRequest.ContinuationData?.Token, result, cacheEntryOptions);
        }
      
        return cache;
    }

    public async Task<ResultObject<List<MusicSearchResult>>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        if (!_memoryCache.TryGetValue("releases", out ResultObject<List<MusicSearchResult>> cache))
        {
            ResultObject<List<MusicSearchResult>> result = new ResultObject<List<MusicSearchResult>>(
                (await _releasesClient.GetReleasesAsync(cancellationToken))
                .Select(release => new MusicSearchResult
                {
                    ImageUrl = release.Thumbnails.LastOrDefault()?.Url,
                    Title = release.Title,
                    Author = release.Author?.ToString(),
                    YouTubeMusicPlaylistUrl = release.Url,
                    RecordType = release.RecordType,
                    Year = int.TryParse(release.Year, out int year) ? year : null,
                }).ToList());

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set("releases", result, cacheEntryOptions);
        }

        return cache;
    }

    public async Task<PagingResult<ArtistSearchResult>> SearchArtistsAsync(QueryRequest queryRequest,
        CancellationToken cancellationToken)
    {
        if (queryRequest.ContinuationNeed == true || !_memoryCache.TryGetValue(queryRequest.Query + queryRequest.ContinuationData?.Token, out PagingResult<ArtistSearchResult> cache))
        {
            var searchResult = (await _searchClient.SearchArtistsChannelsAsync(queryRequest, cancellationToken));
            var result = new PagingResult<ArtistSearchResult>(searchResult.Result.Select(x => new ArtistSearchResult
            {
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                Title = x.Title,
                YouTubeMusicUrl = x.Url
            }))
            {
                ContinuationToken = searchResult.ContinuationToken,
                Token = searchResult.Token
            };

            cache = result;

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromHours(1));

            _memoryCache.Set(queryRequest.Query + queryRequest.ContinuationData?.Token, result, cacheEntryOptions);
        }

        return cache;
    }
}