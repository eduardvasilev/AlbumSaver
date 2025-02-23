using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace YTMusicDownloader.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // webBuilder.UseSentry(o =>
                    // {
                    //     o.Dsn = "https://8118f6f5f18cfc04243284bfe7af4e35@o4508851973849088.ingest.de.sentry.io/4508851976339536";
                    //     // When configuring for the first time, to see what the SDK is doing:
                    //     o.Debug = true;
                    //     o.CaptureFailedRequests = true;
                    // });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
