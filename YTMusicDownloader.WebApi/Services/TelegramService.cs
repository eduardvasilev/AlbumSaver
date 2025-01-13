using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using YoutubeExplode;
using YoutubeExplode.Bridge;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YTMusicDownloader.WebApi.Model;

namespace YTMusicDownloader.WebApi.Services
{
    public class Referal
    {
        public Referal(string title, string url)
        {
            Title = title;
            Url = url;
        }
        public string Title { get; set; }

        public string Url { get; set; }
    }
    public class TelegramService : ITelegramService
    {
        private readonly IUpdateService _updateService;
        private readonly IBotService _botService;
        private readonly IBackupBackendService _backupBackendService;
        private readonly IPaymentService _paymentService;
        private readonly YoutubeClient _youtubeClient;

        private readonly List<Referal> referals = new List<Referal>
        {
            new("My Society", "https://t.me/TheMySocietyBot?start=_tgr_-yDbr04yNzcy"),
            new("#solohash", "https://t.me/Solohashbot?start=_tgr_5pDPj7NkY2Yy"),
            new("#StarsHash", "https://t.me/starshash_bot?start=_tgr_7crlyuVjNTg6"),
        };

        private Random _random;

        public TelegramService(IUpdateService updateService, 
            IBotService botService,
            IBackupBackendService backupBackendService,
            IPaymentService paymentService
            )
        {
            _updateService = updateService;
            _botService = botService;
            _backupBackendService = backupBackendService;
            _paymentService = paymentService;
            _youtubeClient = new YoutubeClient();
            _random = new Random();
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


            result.AlbumImage = album.Thumbnails.Skip(1).FirstOrDefault()?.Url;
            result.AlbumImageConst = (videos.FirstOrDefault()?.Thumbnails)?.MaxBy(x => x.Resolution.Width)?.Url;
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

            var next = _random.Next(0, 100);
            bool sendRefereal = next < 30;
            if (videos.Any())
            {
                if (!string.IsNullOrWhiteSpace(thumbnail))
                {
                    if (!sendRefereal)
                    {
                        await _botService.Client.SendPhotoAsync(request.UserId,
                            inputOnlineFile, caption: $"{result.Author} - {result.Title}");
                    }
                    else
                    {
                        var urlButton = GetRefereal();

                        await _botService.Client.SendPhotoAsync(request.UserId,
                            inputOnlineFile, caption: $"{result.Author} - {result.Title}", replyMarkup: new InlineKeyboardMarkup(urlButton));
                    }
                }

                foreach (PlaylistVideo playlistVideo in videos)
                {
                    await _updateService.SendSongAsync(request.UserId, playlistVideo, thumb);
                }
            }

            if (!sendRefereal)
            {
                await _paymentService.SendDonateMessageAsync(request.UserId);
            }
        }

        private InlineKeyboardButton GetRefereal()
        {
            var referealIndex = _random.Next(referals.Count);
            var referal = referals[referealIndex];
            InlineKeyboardButton urlButton = new InlineKeyboardButton(referal.Title);
            urlButton.Url = referal.Url;
            return urlButton;
        }

        public async Task SendTrackAsync(DownloadRequest request)
        {
            var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(request.YouTubeMusicPlaylistUrl));


            InputFileUrl thumb = new InputFileUrl(result.Thumbnails.FirstOrDefault()?.Url);
            await _updateService.SendSongAsync(request.UserId, result, thumb);
            await _paymentService.SendDonateMessageAsync(request.UserId);
        }

        public async Task SendTracksSetAsync(DownloadSetRequest request)
        {
            foreach (var requestUrl in request.Urls)
            {
                var result = await _youtubeClient.Videos.GetAsync(VideoId.Parse(requestUrl));

                InputFileUrl thumb = new InputFileUrl(result.Thumbnails.FirstOrDefault()?.Url);

                await _updateService.SendSongAsync(request.UserId, result, thumb);
            }

            await _paymentService.SendDonateMessageAsync(request.UserId);
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
