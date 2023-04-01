using System.Collections.Generic;

namespace YTMusicDownloader.WebApi.Model
{
    public class PagingResult<T>
    {
        public IEnumerable<T> Result { get; set; }
        public string ContinuationToken { get; set; }
        public string Token { get; set; }
    }
}
