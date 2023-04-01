using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode.Search;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public interface ITelegramService
    {
        Task<PagingResult<MusicSearchResult>> Search(string query, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken = default);
        Task<PagingResult<MusicSearchResult>> SearchTracks(string query, bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken = default);

        Task<ResultObject<IEnumerable<MusicSearchResult>>> GetTracksByAlbumAsync(string albumUrl, CancellationToken cancellationToken);

        Task<ResultObject<IEnumerable<MusicSearchResult>>> GetTracksByArtistAsync(string channelUrl,
            CancellationToken cancellationToken);

        Task<PagingResult<ArtistSearchResult>> GetArtists(string query,
            bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken);

        Task SendAlbumAsync(DownloadRequest request);

        Task SendTrackAsync(DownloadRequest request);

        Task<ResultObject<List<MusicSearchResult>>> GetReleases(CancellationToken cancellationToken);
    }
}
