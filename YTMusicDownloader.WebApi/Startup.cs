using System.Text.Json.Serialization;
using AlbumSaver.MetricCollector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using YTMusicDownloader.WebApi.Services;

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
            services.AddScoped<IUpdateService, UpdateService>();
            services.AddSingleton<IBotService, BotService>();
            services.AddScoped<ITelegramService, TelegramService>();
            services.Configure<BotConfiguration>(Configuration.GetSection("BotConfiguration"));
            services.Configure<MetricCollectionConfiguration>(Configuration.GetSection("MetricCollectionConfiguration"));

            //try
            //{
            //    services.AddScoped(provider =>
            //    {
            //        string connectionString = Configuration["Database:ConnectionString"];
            //        MongoClient client = new MongoClient(connectionString);
            //        return client.GetDatabase(Configuration["Database:DatabaseName"]);
            //    });

            //    services.AddScoped<DownloadMetricService>();
            //}
            //catch
            //{
            //    //
            //}

            services.AddControllers()
                .AddNewtonsoftJson();

            services.AddHttpClient();
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


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
