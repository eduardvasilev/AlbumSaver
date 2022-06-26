using System.Collections.Generic;
using Newtonsoft.Json;

namespace YTMusicDownloader.WebApi.Model
{
    public class Author
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class Feed
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("author")]
        public Author Author { get; set; }

        [JsonProperty("links")]
        public List<Link> Links { get; set; }

        [JsonProperty("copyright")]
        public string Copyright { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("results")]
        public List<Result> Results { get; set; }
    }

    public class Genre
    {
        [JsonProperty("genreId")]
        public string GenreId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class Link
    {
        [JsonProperty("self")]
        public string Self { get; set; }
    }

    public class Result
    {
        [JsonProperty("artistName")]
        public string ArtistName { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("releaseDate")]
        public string ReleaseDate { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("artistId")]
        public string ArtistId { get; set; }

        [JsonProperty("artistUrl")]
        public string ArtistUrl { get; set; }

        [JsonProperty("contentAdvisoryRating")]
        public string ContentAdvisoryRating { get; set; }

        [JsonProperty("artworkUrl100")]
        public string ArtworkUrl100 { get; set; }

        [JsonProperty("genres")]
        public List<Genre> Genres { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class ItunesMostRecentModel
    {
        [JsonProperty("feed")]
        public Feed Feed { get; set; }
    }

}