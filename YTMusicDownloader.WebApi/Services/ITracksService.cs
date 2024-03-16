using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public interface ITracksService
{
    Task<ResultObject<IEnumerable<MusicSearchResult>>> GetAlbumTracksAsync(string albumUrl,
        CancellationToken cancellationToken);
}