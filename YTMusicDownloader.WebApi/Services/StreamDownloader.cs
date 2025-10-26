using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YTMusicDownloader.WebApi.Services
{
    public class StreamDownloader
    {
        private readonly HttpClient _httpClient;

        public StreamDownloader(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        }

        /// <summary>
        /// Скачивает аудиопоток сегментами (по Range) в MemoryStream.
        /// </summary>
        public async Task<MemoryStream> DownloadInChunksAsync(
            string url,
            int chunkSize = 1_000_000,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // HttpClient живёт вне метода — не трогаем
            var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(headRequest, cancellationToken);
            headResponse.EnsureSuccessStatusCode();

            var totalLength = headResponse.Content.Headers.ContentLength ?? -1;
            if (totalLength <= 0)
                throw new InvalidOperationException("Сервер не сообщил длину файла.");

            var resultStream = new MemoryStream(capacity: (int)totalLength);
            long totalRead = 0;

            while (totalRead < totalLength)
            {
                var rangeStart = totalRead;
                var rangeEnd = Math.Min(rangeStart + chunkSize - 1, totalLength - 1);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(rangeStart, rangeEnd);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var chunkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await chunkStream.CopyToAsync(resultStream, 81920, cancellationToken);

                totalRead += (rangeEnd - rangeStart + 1);
                progress?.Report((double)totalRead / totalLength);
            }

            resultStream.Position = 0;
            return resultStream; // ❗ вызывающий Dispose делает сам
        }

    }
}