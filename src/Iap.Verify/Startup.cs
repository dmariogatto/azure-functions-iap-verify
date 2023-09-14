using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Text;

[assembly: FunctionsStartup(typeof(Iap.Verify.Startup))]

namespace Iap.Verify
{
    public class Startup : FunctionsStartup
    {
        public const string GraceDaysKey = "GraceDays";

        public override void Configure(IFunctionsHostBuilder builder)
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
                var googleOptions = serviceProvider.GetService<IOptions<GoogleOptions>>().Value;
                var base64EncodedBytes = Convert.FromBase64String(googleOptions.KeyBase64);
                var key = Encoding.UTF8.GetString(base64EncodedBytes);

                return new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(googleOptions.Account)
                    {
                        Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
                    }.FromPrivateKey(key)
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

            builder.Services.AddOptions<AppleSecretOptions>()
                .Configure(builder.GetContext().Configuration.GetSection(AppleSecretOptions.AppleSecretStoreKey).Bind);
            builder.Services.AddOptions<AppleStoreOptions>()
                .Configure(builder.GetContext().Configuration.GetSection(AppleStoreOptions.AppleStoreKey).Bind);
            builder.Services.AddOptions<GoogleOptions>()
                .Configure(builder.GetContext().Configuration.GetSection(GoogleOptions.GoogleKey).Bind);

            builder.Services.AddHttpClient();
            builder.Services.AddLogging();

            builder.Services.AddSingleton(services =>
                new TableStorageOptions()
                {
                    AzureWebJobsStorage = services.GetService<IConfiguration>().GetValue<string>("AzureWebJobsStorage")
                });

            builder.Services.AddSingleton<IVerificationRepository, VerificationRepository>();
        }
    }
}
