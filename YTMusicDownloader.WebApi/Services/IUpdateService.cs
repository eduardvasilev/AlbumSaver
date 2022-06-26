using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace YTMusicDownloader.WebApi.Services
{
    public interface IUpdateService
    {
        Task ProcessAsync(Update update, CancellationToken cancellationToken);
    }
}
