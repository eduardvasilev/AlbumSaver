using System.Threading;
using System.Threading.Tasks;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public interface IArtistsService
{
    Task<UrlModel> GetArtistImageAsync(string channelUrl,
        CancellationToken cancellationToken);
}