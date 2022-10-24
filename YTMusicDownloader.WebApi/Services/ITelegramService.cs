using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public interface ITelegramService
    {
        Task<IEnumerable<YTMusicSearchResult>> Search(string query, int page, CancellationToken cancellationToken = default);
        Task<IEnumerable<YTMusicSearchResult>> SearchTracks(string query, int page,
            CancellationToken cancellationToken = default);
        Task SendAlbumAsync(DownloadRequest request);
        Task SendTrackAsync(DownloadRequest request);
    }
}
