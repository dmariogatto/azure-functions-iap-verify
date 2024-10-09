using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json.Serialization;
using JsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddSingleton(serviceProvider =>
        {
            return new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .Build();
        });

        services.AddSingleton(serviceProvider =>
        {
            var googleOptions = serviceProvider.GetService<IOptions<GoogleOptions>>().Value;
            var base64EncodedBytes = Convert.FromBase64String(googleOptions.KeyBase64);
            var key = Encoding.UTF8.GetString(base64EncodedBytes);

            return new ServiceAccountCredential(
                new ServiceAccountCredential.Initializer(googleOptions.Account)
                {
                    Scopes = [AndroidPublisherService.Scope.Androidpublisher]
                }.FromPrivateKey(key)
            );
        });

        services.AddSingleton(serviceProvider =>
        {
            return new AndroidPublisherService(
                new BaseClientService.Initializer
                {
                    HttpClientInitializer = serviceProvider.GetService<ServiceAccountCredential>(),
                    ApplicationName = "Azure Function",
                }
            );
        });

        services.AddOptions<IapOptions>()
            .Configure<IConfiguration>((settings, config) => config.GetSection(IapOptions.IapKey).Bind(settings));
        services.AddOptions<AppleSecretOptions>()
            .Configure<IConfiguration>((settings, config) => config.GetSection(AppleSecretOptions.AppleSecretStoreKey).Bind(settings));
        services.AddOptions<AppleStoreOptions>()
            .Configure<IConfiguration>((settings, config) => config.GetSection(AppleStoreOptions.AppleStoreKey).Bind(settings));
        services.AddOptions<GoogleOptions>()
            .Configure<IConfiguration>((settings, config) => config.GetSection(GoogleOptions.GoogleKey).Bind(settings));

        services.AddHttpClient();

        services.AddSingleton(services =>
            new TableStorageOptions()
            {
                AzureWebJobsStorage = services.GetService<IConfiguration>().GetValue<string>("AzureWebJobsStorage")
            });

        services.AddSingleton<IVerificationRepository, VerificationRepository>();
    })
    .Build();

host.Run();
