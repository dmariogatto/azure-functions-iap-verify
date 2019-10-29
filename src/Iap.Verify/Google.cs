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
using Microsoft.WindowsAzure.Storage.Table;
using Iap.Verify.Tables;

namespace Iap.Verify
{
    public static class Google
    {
        private const string GooglePlayAccount = Secrets.GoogleAccount;
        private const string GooglePlayKey = Secrets.GoogleKey;

        private static readonly ServiceAccountCredential _credential = new ServiceAccountCredential
        (
            new ServiceAccountCredential.Initializer(GooglePlayAccount)
            {
                Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
            }.FromPrivateKey(GooglePlayKey)
        );

        private static readonly AndroidPublisherService _googleService = new AndroidPublisherService
        (
            new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "Azure Function",
            }
        );

        [FunctionName(nameof(Google))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [Table(nameof(Google))] CloudTable verificationTable,
            ILogger log)
        {
            var receipt = default(Receipt);
            var result = default(ValidationResult);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                receipt = JsonConvert.DeserializeObject<Receipt>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to parse {nameof(Receipt)}: {ex.Message}", ex);
            }

            if (!string.IsNullOrEmpty(receipt?.BundleId) &&
                !string.IsNullOrEmpty(receipt?.ProductId) &&
                !string.IsNullOrEmpty(receipt?.TransactionId) &&
                !string.IsNullOrEmpty(receipt?.DeveloperPayload) &&
                !string.IsNullOrEmpty(receipt?.Token))
            {
                try
                {
                    var product = await _googleService.Inappproducts.Get(receipt.BundleId, receipt.ProductId).ExecuteAsync();

                    if (product != null)
                    {
                        result = product.PurchaseType == "subscription"
                            ? await ValidateSubscription(receipt, log)
                            : await ValidateProduct(receipt, log);
                    }
                    else
                    {
                        result = new ValidationResult(false, $"IAP '{receipt.BundleId}':'{receipt.ProductId}' not found");
                    }                    
                }
                catch (Exception ex)
                {
                    log.LogError($"Failed to validate IAP: {ex.Message}", ex);
                    result = new ValidationResult(false, ex.Message);
                }
            }
            else
            {
                result = new ValidationResult(false, $"Invalid {nameof(Receipt)}");
            }

            await Storage.SaveLog(verificationTable, receipt, result, log);

            if (result.IsValid)
            {
                log.LogInformation($"Validated IAP '{receipt.BundleId}':'{receipt.ProductId}'");
                return new OkResult();
            }

            if (!string.IsNullOrEmpty(receipt?.BundleId) &&
                !string.IsNullOrEmpty(receipt?.ProductId))
            {
                log.LogInformation($"Failed to validate IAP '{receipt.BundleId}':'{receipt.ProductId}', reason '{result?.Message ?? string.Empty}'");
            }
            else
            {
                log.LogInformation($"Failed to validate IAP, reason '{result?.Message ?? string.Empty}'");
            }

            return new BadRequestResult();
        }

        private static async Task<ValidationResult> ValidateProduct(Receipt receipt, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Products.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchase = await request.ExecuteAsync();

                if (purchase != null)
                {
                    receipt.Environment = purchase.PurchaseType == 0
                        ? "Test" : "Production";
                }

                if (purchase.DeveloperPayload != receipt.DeveloperPayload)
                {
                    result = new ValidationResult(false, "DeveloperPayload did not match");
                }
                else if (purchase.PurchaseState != 0)
                {
                    result = new ValidationResult(false, "purchase was cancelled or refunded");
                }
                else
                {
                    result = new ValidationResult(true);
                }
            }
            catch (Exception ex)
            {
                log.LogError("Failed to validate product", ex);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }

        private static async Task<ValidationResult> ValidateSubscription(Receipt receipt, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Subscriptions.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchase = await request.ExecuteAsync();

                if (purchase != null)
                {
                    receipt.Environment = purchase.PurchaseType == 0
                        ? "Test" : "Production";
                }

                if (purchase?.DeveloperPayload != receipt.DeveloperPayload)
                {
                    result = new ValidationResult(false, "DeveloperPayload did not match");
                }
                else if (!purchase.ExpiryTimeMillis.HasValue ||
                         DateTime.UnixEpoch
                                 .AddMilliseconds(purchase.ExpiryTimeMillis.Value)
                                 .AddDays(3).Date <= DateTime.UtcNow.Date)
                {
                    result = new ValidationResult(false, $"subscription expiried {purchase.ExpiryTimeMillis ?? -1}");
                }
                else
                {
                    result = new ValidationResult(true);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to validate subscription: {ex.Message}", ex);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }        
    }
}
