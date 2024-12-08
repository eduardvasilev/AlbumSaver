namespace YTMusicDownloader.WebApi.Model;

public class AlbumTracksResultObject<T> : ResultObject<T>
{
    public AlbumTracksResultObject(T result) : base(result)
    {
        Result = result;
    }
    public T Result { get; set; }

    public string AlbumTitle { get; set; }
    public string ArtistName { get; set; }
    public string AlbumImage { get; set; }
    public string AlbumImageConst { get; set; }
    public string ChannelUrl { get; set; }
}