using System.Threading.Tasks;

namespace YTMusicDownloader.WebApi.Services;

public interface IPaymentService
{
    Task SendDonateMessageAsync(long userId);
}