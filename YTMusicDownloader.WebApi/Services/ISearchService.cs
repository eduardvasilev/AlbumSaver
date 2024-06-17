using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YTMusicAPI.Model;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public interface ISearchService
{
    Task<PagingResult<MusicSearchResult>> SearchAlbumsAsync(QueryRequest queryRequest, CancellationToken cancellationToken);
    Task<PagingResult<MusicSearchResult>> SearchTracksAsync(QueryRequest queryRequest, CancellationToken cancellationToken);
    Task<ResultObject<List<MusicSearchResult>>> GetReleasesAsync(CancellationToken cancellationToken);

    Task<PagingResult<ArtistSearchResult>> SearchArtistsAsync(QueryRequest queryRequest, 
        CancellationToken cancellationToken);
}