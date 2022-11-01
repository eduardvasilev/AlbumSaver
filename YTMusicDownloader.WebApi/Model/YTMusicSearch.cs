using Newtonsoft.Json;

namespace YTMusicDownloader.WebApi.Model
{
    public class YTMusicSearchResult
    {
        public string YouTubeMusicPlaylistUrl { get; set; }
        public string Title { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Author { get; set; }
        public string ImageUrl { get; set; }
    }
}
