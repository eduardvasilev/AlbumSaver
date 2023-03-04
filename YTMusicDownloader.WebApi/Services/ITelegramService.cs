using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public interface ITelegramService
    {
        Task<PagingResult> Search(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken = default);
        Task<PagingResult> SearchTracks(string query, bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken = default);

        Task<ResultObject<IEnumerable<YTMusicSearchResult>>> GetTracksByAlbumAsync(string albumUrl, CancellationToken cancellationToken);

        Task SendAlbumAsync(DownloadRequest request);

        Task SendTrackAsync(DownloadRequest request);

        Task<ResultObject<List<YTMusicSearchResult>>> GetReleases(CancellationToken cancellationToken);
    }
}
