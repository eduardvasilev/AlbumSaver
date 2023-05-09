using System.Collections.Generic;

namespace YTMusicDownloader.WebApi.Model
{
    public class DownloadSetRequest
    {
        public long UserId { get; set; }
        public List<string> Urls { get; set; }
    }
}
