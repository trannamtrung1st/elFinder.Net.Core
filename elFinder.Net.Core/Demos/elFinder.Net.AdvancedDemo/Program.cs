using elFinder.Net.AdvancedDemo.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace elFinder.Net.AdvancedDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>())
            {
                dbContext.AddRange(new AppUser
                {
                    Id = 1,
                    UserName = "mrdavid",
                    VolumePath = "mrdavid",
                    QuotaInBytes = 10 * (long)Math.Pow(1024, 2),
                }, new AppUser
                {
                    Id = 2,
                    UserName = "msdiana",
                    VolumePath = "msdiana",
                    QuotaInBytes = (long)Math.Pow(1024, 3),
                });

                dbContext.SaveChanges();
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
