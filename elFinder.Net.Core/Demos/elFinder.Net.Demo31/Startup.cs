using elFinder.Net.AspNetCore.Extensions;
using elFinder.Net.Core;
using elFinder.Net.Demo31.Volumes;
using elFinder.Net.Drivers.FileSystem.Extensions;
using elFinder.Net.Drivers.FileSystem.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace elFinder.Net.Demo31
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            WebRootPath = env.WebRootPath;
        }

        public static string WebRootPath { get; private set; }

        public static string MapPath(string path, string basePath = null)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = WebRootPath;
            }

            path = path.Replace("~/", "").TrimStart('/').Replace('/', '\\');
            return PathHelper.GetFullPath(basePath, path);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            #region elFinder
            services.AddElFinderAspNetCore()
                .AddFileSystemDriver();

            services.AddTransient<IVolume>(provider =>
            {
                var driver = provider.GetRequiredService<IDriver>();
                var volume = new Volume1(driver,
                    MapPath("~/upload"), $"/upload/", $"/api/files/thumb/")
                {
                    StartDirectory = MapPath("~/upload/start"),
                    Name = "Volume 1",
                    ThumbnailDirectory = PathHelper.GetFullPath("./thumb")
                };
                return volume;
            });

            services.AddTransient<IVolume>(provider =>
            {
                var driver = provider.GetRequiredService<IDriver>();
                var volume = new Volume2(driver,
                    MapPath("~/upload-2"), $"/upload-2/", $"/api/files/thumb/")
                {
                    StartDirectory = MapPath("~/upload-2/start"),
                    Name = "Volume 2"
                };
                return volume;
            });
            #endregion

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);

            services.AddRazorPages();
            services.AddControllersWithViews().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IEnumerable<IVolume> volumes)
        {
            // elFinder
            var setupTasks = volumes.Select(async volume => await volume.Driver.SetupVolumeAsync(volume)).ToArray();
            Task.WaitAll(setupTasks);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseResponseCompression();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
