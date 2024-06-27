using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class BackupBackendService : IBackupBackendService
{
    private HttpClient _client;

    public BackupBackendService(IHttpClientFactory httpClientFactory, IOptions<BackupBackendOptions> options)
    {
        _client = httpClientFactory.CreateClient();
        _client.BaseAddress = new Uri(options.Value.BaseUrl);

    }
    public async Task<bool> TrySendMusicAsync(long userId, string musicUrl, EntityType entityType)
    {
        var response = await  _client.PostAsync($"/download?youTubeMusicPlaylistUrl={musicUrl}&userId={userId}&entityType={entityType}",
            null);
        return response.IsSuccessStatusCode;
    }
}

public class BackupBackendOptions
{
    public string BaseUrl { get; set; }
}