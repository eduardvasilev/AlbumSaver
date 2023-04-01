using Newtonsoft.Json;

namespace YTMusicDownloader.WebApi.Model
{
    public class MusicSearchResult
    {
        public string YouTubeMusicPlaylistUrl { get; set; }
        public string Title { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Author { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Year { get; set; }


        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? RecordType { get; set; }
        public string ImageUrl { get; set; }
    }
}
