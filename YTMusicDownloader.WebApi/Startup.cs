using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System;
using YTMusicAPI;
using YTMusicAPI.Abstraction;
using YTMusicDownloader.WebApi.Services;
using YTMusicDownloader.WebApi.Services.Telegram;
using YoutubeExplode;
using YTMusicDownloader.WebApi.Services.Streams;
using YTMusicDownloader.WebApi.Services.Streams.Abstraction;
using YTMusicDownloader.WebApi.Services.Streams.Providers;

namespace YTMusicDownloader.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddScoped<IUpdateService, UpdateService>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<ITracksService, TracksService>();
            services.AddScoped<IReleasesClient, ReleasesClient>();
            services.AddScoped<IArtistsService, ArtistsService>();
            services.AddSingleton<IBotService, BotService>();
            services.AddScoped<ISearchClient, SearchClient>();
            services.AddScoped<ITrackClient, TrackClient>();
            services.AddScoped<IArtistClient, ArtistClient>();
            services.AddScoped<IDownloadService, DownloadService2>();
            
            services.AddScoped<IStreamProvider, ExplodeProvider>();
            services.AddScoped<IStreamProvider, YTMusicProvider>();
            services.AddScoped<StreamFactory>();

            services.AddTransient<YoutubeClient>();
            services.AddScoped<IBackupBackendService, BackupBackendService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.Configure<BotConfiguration>(Configuration.GetSection("BotConfiguration"));
            services.Configure<BackupBackendOptions>(Configuration.GetSection("BackupBackend"));
            services.Configure<PaymentOptions>(Configuration.GetSection("Payment"));
            var redisConfig = Configuration.GetSection("Redis");
            services.Configure<RedisOptions>(redisConfig);

            if (redisConfig.GetValue<bool>("Enabled"))
            {
                services.AddSingleton<ITelegramFilesService, TelegramFilesService>();
            }
            else
            {
                services.AddSingleton<ITelegramFilesService, MockTelegramFilesService>();
            }

            services.AddHealthChecks();
            services.AddControllers()
                .AddNewtonsoftJson();

            services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddHttpClient();

            // The following line enables Application Insights telemetry collection.
            services.AddApplicationInsightsTelemetry();

            services.AddMvc();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "AlbumSaver API");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseHealthChecks("/health");
        }
    }
}
