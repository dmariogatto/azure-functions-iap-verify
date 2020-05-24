using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Iap.Verify;
using Iap.Verify.Tables;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(Startup))]
namespace Iap.Verify
{
    public class Startup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddSingleton(serviceProvider =>
            {
                return new ConfigurationBuilder()
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables()
                   .Build();
            });

            builder.Services.AddSingleton(serviceProvider =>
            {
                var config = serviceProvider.GetService<IConfiguration>();
                var storageConnString =
                    config["AzureWebJobsStorage"] ??
                    config.GetConnectionString("StorageConnectionString") ??
                    "UseDevelopmentStorage=true;";
                return CloudStorageAccount.Parse(storageConnString);
            });

            builder.Services.AddSingleton(serviceProvider =>
            {
                var config = serviceProvider.GetService<IConfiguration>();
                return new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(config["GoogleAccount"])
                    {
                        Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
                    }.FromPrivateKey(config["GoogleKey"].Replace("\\n", "\n"))
                );
            });

            builder.Services.AddSingleton(serviceProvider =>
            {
                return new AndroidPublisherService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer = serviceProvider.GetService<ServiceAccountCredential>(),
                        ApplicationName = "Azure Function",
                    }
                );
            });

            builder.Services.AddSingleton(serviceProvider =>
            {
                var storageAccount = serviceProvider.GetService<CloudStorageAccount>();
                return storageAccount.CreateCloudTableClient();
            });

            builder.Services.AddHttpClient();
            builder.Services.AddLogging();

            builder.Services.AddSingleton<IVerificationRepository, VerificationRepository>();
        }
    }
}
