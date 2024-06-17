using System.Threading;
using System.Threading.Tasks;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public interface IDownloadService
{
    Task SendAlbumAsync(DownloadRequest request, CancellationToken cancellationToken);
    Task SendTrackAsync(DownloadRequest model, CancellationToken cancellationToken);
    Task SendTracksSetAsync(DownloadSetRequest request, CancellationToken cancellationToken);
}