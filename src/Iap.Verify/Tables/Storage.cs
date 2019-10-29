using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace Iap.Verify.Tables
{
    public static class Storage
    {
        private static IConfigurationRoot Configuration = new ConfigurationBuilder()
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables()
                   .Build();

        public static CloudTable GetAppleTable()
        {
            return GetClient().GetTableReference("Apple");
        }

        public static CloudTable GetGoogleTable()
        {
            return GetClient().GetTableReference("Google");
        }

        private static CloudTableClient GetClient()
        {
            var storageConnString =
                Configuration["Values:AzureWebJobsStorage"] ??
                Configuration.GetConnectionString("StorageConnectionString") ??
                "UseDevelopmentStorage=true;";
            var storageAccount = CloudStorageAccount.Parse(storageConnString);
            return storageAccount.CreateCloudTableClient();
        }
    }
}
