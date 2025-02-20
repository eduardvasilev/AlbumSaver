using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Sentry;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services;

public class BackupBackendService : IBackupBackendService
{
    private readonly IOptions<BackupBackendOptions> _options;
    private HttpClient _client;

    public BackupBackendService(IHttpClientFactory httpClientFactory, IOptions<BackupBackendOptions> options)
    {
        _options = options;
        _client = httpClientFactory.CreateClient();
        _client.BaseAddress = new Uri(options.Value.BaseUrl);

    }
    public async Task<bool> TrySendMusicAsync(long userId, string musicUrl, EntityType entityType)
    {
        if (_options.Value.Enabled)
        {
            SentrySdk.CaptureMessage("Going to backup service");
            var response = await _client.PostAsync($"/download?youTubeMusicPlaylistUrl={musicUrl}&userId={userId}&entityType={entityType}",
                null);
            return response.IsSuccessStatusCode;
        }
        return false;
    }
}

public class BackupBackendOptions
{
    public string BaseUrl { get; set; }
    public bool Enabled { get; set; }
}