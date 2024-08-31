using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using YoutubeExplode;
using YoutubeExplode.Bridge;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YTMusicDownloader.WebApi.Model;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;

namespace YTMusicDownloader.WebApi.Services
{
    public class TelegramService : ITelegramService
    {
        private readonly IUpdateService _updateService;
        private readonly IBotService _botService;
        private readonly IBackupBackendService _backupBackendService;
        private readonly YoutubeClient _youtubeClient;

        public TelegramService(IUpdateService updateService, 
            IBotService botService,
            IBackupBackendService backupBackendService
            )
        {
            _updateService = updateService;
            _botService = botService;
            _backupBackendService = backupBackendService;
            _youtubeClient = new YoutubeClient();
        }

        public async Task<PagingResult<MusicSearchResult>> Search(string query,
            bool continuation, 
            string continuationToken,
            string token,
            CancellationToken cancellationToken)
        {

            ContinuationData continuationData = continuation ? new ContinuationData
            {
                ContinuationToken = new JValue(continuationToken),
                Token = new JValue(token)
            } : null;

            var playlistSearchResults = await _youtubeClient.Search
                .GetPlaylistsAsync(query, continuation, continuationData, cancellationToken);

            return new PagingResult<MusicSearchResult>
            {
                Result = playlistSearchResults
                    .Select(result => new MusicSearchResult
                    {
                        ImageUrl = result.Thumbnails.Last().Url,
                        Title = result.Title,
                        Author = result.Author?.ToString(),
                        YouTubeMusicPlaylistUrl = result.Url,
                        Year = int.TryParse(result.Year, out int intYear) ? intYear : null,
                        RecordType = result.EntryType

                    }).ToList(),
                Token = _youtubeClient.Search.Token != null ? _youtubeClient.Search.Token.Value<string>() : null,
                ContinuationToken = _youtubeClient.Search.ContinuationToken != null
                    ? _youtubeClient.Search.ContinuationToken.Value<string>()
                    : null,
            };
        }

        public async Task<PagingResult<MusicSearchResult>> SearchTracks(string query,
            bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken)
        {

            ContinuationData continuationData = continuation ? new ContinuationData
            {
                ContinuationToken = new JValue(continuationToken),
                Token = new JValue(token)
            } : null;

            var videoSearchResults = (await _youtubeClient.Search
                .GetVideosAsync(query, continuation, continuationData, cancellationToken));

            return new PagingResult<MusicSearchResult>
            {
                Result = videoSearchResults
                    .Select(result => new MusicSearchResult
                    {
                        ImageUrl = result.Thumbnails.LastOrDefault()?.Url,
                        Title = result.Title,
                        Author = result.Author.ToString(),
                        YouTubeMusicPlaylistUrl = result.Url,
                    }).ToList(),
                Token = _youtubeClient.Search.Token?.Value<string>(),
                ContinuationToken = _youtubeClient.Search.ContinuationToken != null
                    ? _youtubeClient.Search.ContinuationToken.Value<string>()
                    : null,
            };
        }

        public async Task<ResultObject<IEnumerable<MusicSearchResult>>> GetTracksByAlbumAsync(string albumUrl, CancellationToken cancellationToken)
        {
            Playlist album = await _youtubeClient.Playlists.GetAsync(albumUrl, cancellationToken);

            var videos =
                await _youtubeClient.Playlists.GetVideosAsync(PlaylistId.Parse(albumUrl));

            var result = new AlbumTracksResultObject<IEnumerable<MusicSearchResult>>(videos.Select(x => new MusicSearchResult
            {
                Author = x.Author.ToString(),
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                Title = x.Title,
                YouTubeMusicPlaylistUrl = x.Url,
            }));


            result.AlbumImage = album.Thumbnails.LastOrDefault()?.Url;
            result.AlbumTitle = album.Title;
            result.ChannelUrl = album.Author?.ChannelId;
            result.ArtistName = album.Author?.Title;
            return result;
        }

        public async Task<PagingResult<MusicSearchResult>> GetTracksByArtistAsync(string channelUrl, bool continuation,
            string continuationToken,
            string token, CancellationToken cancellationToken)
        {
            var videos =
                await _youtubeClient.Playlists.GetByArtistAsync(ChannelId.Parse(channelUrl), _youtubeClient, continuation, continuationToken, token, cancellationToken);

            return new PagingResult<MusicSearchResult>(videos.Select(x => new MusicSearchResult
            {
                Author = x.Author.ToString(),
                ImageUrl = x.Thumbnails.LastOrDefault()?.Url,
                Title = x.Title,
                YouTubeMusicPlaylistUrl = x.Url,
            }))
            {
                Token = _youtubeClient.Search.Token?.Value<string>(),
                ContinuationToken = _youtubeClient.Search.ContinuationToken?.Value<string>(),
            };
        }

        public async Task<ResultObject<IEnumerable<MusicSearchResult>>> GetAlbumsByArtistAsync(string channelUrl, CancellationToken cancellationToken)
        {
            var albums =
                await _youtubeClient.Playlists.GetAlbumsByArtistAsync(ChannelId.Parse(channelUrl), cancellationToken);

            return new ResultObject<IEnumerable<MusicSearchResult>>(albums.Select(x => new MusicSearchResult
            {
                Author = x.Author.ToString(),
                ImageUrl = x.Thumbnails.FirstOrDefault()?.Url,
                Title = x.Title,
                YouTubeMusicPlaylistUrl = x.Url,
            }));
        }

        public async Task<UrlModel> GetArtistImageAsync(string channelUrl, CancellationToken cancellationToken)
        {
            return new UrlModel(await  _youtubeClient.Channels.GetArtistImage(channelUrl, cancellationToken));
        }


        public async Task<PagingResult<ArtistSearchResult>> GetArtists(string query,
            bool continuation,
            string continuationToken,
            string token,
            CancellationToken cancellationToken)
        {
            ContinuationData continuationData = continuation ? new ContinuationData
            {
                ContinuationToken = new JValue(continuationToken),
                Token = new JValue(token)
            } : null;

            var channelSearchResults = (await _youtubeClient.Search
                .GetChannelsAsync(query, continuation, continuationData, cancellationToken))
                .Select(artist => new ArtistSearchResult
                {
                    Title = artist.Title,
                    ImageUrl = artist.Thumbnails.LastOrDefault()?.Url,
                    YouTubeMusicUrl = artist.Url,
                });

            return new PagingResult<ArtistSearchResult>
            {
                Result = channelSearchResults,
                Token = _youtubeClient.Search.Token?.Value<string>(),
                ContinuationToken = _youtubeClient.Search.ContinuationToken?.Value<string>(),
            };

        }

        public async Task SendAlbumAsync(DownloadRequest request)
        {
            Playlist result =
                (await _youtubeClient.Playlists.GetAsync(PlaylistId.Parse(request.YouTubeMusicPlaylistUrl)));
            IReadOnlyList<PlaylistVideo> videos;
            try
            {
                videos =
                    await _youtubeClient.Playlists.GetVideosAsync(PlaylistId.Parse(request.YouTubeMusicPlaylistUrl));
            }
            catch (Exception e)
            {
                if (!await _backupBackendService.TrySendMusicAsync(request.UserId, request.YouTubeMusicPlaylistUrl, Model.EntityType.Album))
                {
                    throw;
                }
                return;
            }

            string thumbnail = result.Thumbnails.LastOrDefault()?.Url;
            InputFileUrl inputOnlineFile = new InputFileUrl(thumbnail);
            InputFileUrl thumb = new InputFileUrl(result.Thumbnails.FirstOrDefault()?.Url);

            if (videos.Any())
            {
                if (!string.IsNullOrWhiteSpace(thumbnail))
                {
                    await _botService.Client.SendPhotoAsync(request.UserId,
                        inputOnlineFile, caption: $"{result.Author} - {result.Title}");
                }

                foreach (PlaylistVideo playlistVideo in videos)
                {
                    try
                    {
                        await _updateService.SendSongAsync(request.UserId, playlistVideo, thumb);
                    }
                    catch (JsonReaderException)
                    {
                        if (!await _backupBackendService.TrySendMusicAsync(request.UserId, request.YouTubeMusicPlaylistUrl, Model.EntityType.Album))
                        {
                            await _botService.Client.SendTextMessageAsync(request.UserId,
                                $"Sorry, we couldn't send the track: {result.Title}. Our service may be blocked. But we will definitely be back");
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }

            await SendDonateMessage(request.UserId);
        }

        public async Task SendTrackAsync(DownloadRequest request)
        {
            var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(request.YouTubeMusicPlaylistUrl));


            InputFileUrl thumb = new InputFileUrl(result.Thumbnails.FirstOrDefault()?.Url);
            try
            {
                await _updateService.SendSongAsync(request.UserId, result, thumb);
            }
            catch (JsonReaderException)
            {
                if (!await _backupBackendService.TrySendMusicAsync(request.UserId, request.YouTubeMusicPlaylistUrl, Model.EntityType.Track))
                {
                    await _botService.Client.SendTextMessageAsync(request.UserId,
                        $"Sorry, we couldn't send the track: {result.Title}. Our service may be blocked. But we will definitely be back");
                }
                else
                {
                    return;
                }
            }
            await SendDonateMessage(request.UserId);
        }

        public async Task SendTracksSetAsync(DownloadSetRequest request)
        {
            foreach (var requestUrl in request.Urls)
            {
                var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(requestUrl));

                InputFileUrl thumb = new InputFileUrl(result.Thumbnails.FirstOrDefault()?.Url);

                try
                {
                    await _updateService.SendSongAsync(request.UserId, result, thumb);
                }
                catch (JsonReaderException)
                {
                    if (!await _backupBackendService.TrySendMusicAsync(request.UserId, result.Url, Model.EntityType.Track))
                    {
                        await _botService.Client.SendTextMessageAsync(request.UserId,
                            $"Sorry, we couldn't send the track: {result.Title}. Our service may be blocked. But we will definitely be back");
                    }
                    else
                    {
                        return;
                    }
                }
            }

            await SendDonateMessage(request.UserId);
        }

        private async Task SendDonateMessage(long userId)
        {
            await _botService.Client.SendInvoiceAsync(userId, "Buy us a coffee",
                "Support us so we can add new features",
                Guid.NewGuid().ToString(), "", "XTR", new List<LabeledPrice>() { new("Donate us", 1) });
        }

        public async Task<ResultObject<List<MusicSearchResult>>> GetReleases()
        {
            List<MusicSearchResult> releases = (await _youtubeClient.Playlists.GetReleases())
                .Select(release => new MusicSearchResult
                {
                    ImageUrl = release.Thumbnails.LastOrDefault()?.Url,
                    Title = release.Title,
                    Author = release.Author?.ToString(),
                    YouTubeMusicPlaylistUrl = release.Url,
                    RecordType = release.EntryType
                }).ToList();
            return new ResultObject<List<MusicSearchResult>>(releases);
        }
    }
}
