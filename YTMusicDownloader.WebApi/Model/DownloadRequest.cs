namespace YTMusicDownloader.WebApi.Model
{
    public class DownloadRequest
    {
        public long UserId { get; set; }
        public string YouTubeMusicPlaylistUrl { get; set; }
    }
}
