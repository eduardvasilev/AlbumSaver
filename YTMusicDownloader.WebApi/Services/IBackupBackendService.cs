using System.Threading.Tasks;
using EntityType = YTMusicDownloader.WebApi.Model.EntityType;

namespace YTMusicDownloader.WebApi.Services;

public interface IBackupBackendService
{
    Task<bool> TrySendMusicAsync(long userId, string musicUrl, EntityType entityType);
}