namespace YTMusicDownloader.WebApi.Model;

public class ResultObject<T>
{
    public ResultObject(T result)
    {
        Result = result;
    }
    public T Result { get; set; }
}