using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Iap.Verify.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.AndroidPublisher.v3;
using Google.Apis.Services;

namespace Iap.Verify
{
    public static class Google
    {
        private static string GooglePlayAccount = Secrets.GoogleAccount;
        private static string GooglePlayKey = Secrets.GoogleKey;

        private static ServiceAccountCredential _credential = new ServiceAccountCredential
        (
            new ServiceAccountCredential.Initializer(GooglePlayAccount)
            {
                Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
            }.FromPrivateKey(GooglePlayKey)
        );

        private static AndroidPublisherService _googleService = new AndroidPublisherService
        (
            new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "Azure Function",
            }
        );

        [FunctionName(nameof(Google))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var receipt = default(GoogleReceipt);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                receipt = JsonConvert.DeserializeObject<GoogleReceipt>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Failed to parse {nameof(AppleReceipt)}");
            }

            if (string.IsNullOrEmpty(receipt?.BundleId) ||
                string.IsNullOrEmpty(receipt?.ProductId) ||
                string.IsNullOrEmpty(receipt?.TransactionId) ||
                string.IsNullOrEmpty(receipt?.DeveloperPayload) ||
                string.IsNullOrEmpty(receipt?.Token))
            {
                return new BadRequestResult();
            }

            log.LogInformation($"IAP receipt: {receipt.BundleId}, {receipt.TransactionId}");

            try
            {
                var request = _googleService.Purchases.Products.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchaseState = await request.ExecuteAsync();

                if (purchaseState.DeveloperPayload != receipt.DeveloperPayload)
                {
                    log.LogInformation($"IAP invalid, DeveloperPayload did not match!");
                    return new BadRequestResult();
                }
                if (purchaseState.PurchaseState != 0)
                {
                    log.LogInformation($"IAP invalid, purchase was cancelled or refunded!");
                    return new BadRequestResult();
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"IAP invalid, error reported: {ex}");
                return new BadRequestResult();
            }

            log.LogInformation($"IAP Success: {receipt.ProductId}, {receipt.TransactionId}");
            return new OkResult();
        }
    }
}
