namespace YTMusicDownloader.WebApi.Model;

public class UrlModel
{
    public UrlModel()
    {
        
    }
    public UrlModel(string url)
    {
        Url = url;
    }

    public string Url { get; set; }
}