using System.Collections.Generic;

namespace YTMusicDownloader.WebApi.Model
{
    public class PagingResult
    {
        public IEnumerable<YTMusicSearchResult> Result { get; set; }
        public string ContinuationToken { get; set; }
        public string Token { get; set; }
    }
}
