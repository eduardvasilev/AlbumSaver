using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public class TelegramService : ITelegramService
    {
        private readonly YoutubeClient _youtubeClient;

        private const int ElementsPerPageCount = 8;

        public TelegramService()
        {
            _youtubeClient = new YoutubeClient();
        }

        public async Task<IEnumerable<YTMusicSearchResult>> Search(string query, int page, CancellationToken cancellationToken)
        {
            return (await _youtubeClient.Search
                .GetPlaylistsAsync(query, cancellationToken))
                .Skip(ElementsPerPageCount * page)
                .Take(ElementsPerPageCount)
                .Select(result => new YTMusicSearchResult
                {
                    ImageUrl = result.Thumbnails.Last().Url,
                    Title = result.Title,
                    YouTubeMusicPlaylistUrl = result.Url
                }).ToList();
        }
    }
}
