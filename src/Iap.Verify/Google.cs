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
        private static readonly ServiceAccountCredential _credential = new ServiceAccountCredential
        (
            new ServiceAccountCredential.Initializer(Environment.GetEnvironmentVariable("GoogleAccount"))
            {
                Scopes = new[] { AndroidPublisherService.Scope.Androidpublisher }
            }.FromPrivateKey(Environment.GetEnvironmentVariable("GoogleKey").Replace("\\n", "\n"))
        );

        private static readonly AndroidPublisherService _googleService = new AndroidPublisherService
        (
            new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "Azure Function",
            }
        );

        private static int _graceDays = -1;
        public static int GraceDays
        {
            get
            {
                if (_graceDays < 0 &&
                    !int.TryParse(Environment.GetEnvironmentVariable("GraceDays"), out _graceDays))
                {
                    _graceDays = 0;
                }

                return _graceDays;
            }
        }

        [FunctionName(nameof(Google))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] Receipt receipt,
            HttpRequest req,
            [Table(nameof(Google))] CloudTable verificationTable,
            ILogger log)
        {
            var result = default(ValidationResult);

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

            if (result.IsValid && result.ValidatedReceipt != null)
            {
                log.LogInformation($"Validated IAP '{receipt.BundleId}':'{receipt.ProductId}'");
                return new JsonResult(result.ValidatedReceipt);
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

                if (purchase == null)
                {
                    result = new ValidationResult(false, $"no purchase found");
                }
                else if (!purchase.OrderId.StartsWith(receipt.TransactionId))
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{purchase.OrderId}'");
                }
                else if (purchase.DeveloperPayload != receipt.DeveloperPayload)
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
                    result.ValidatedReceipt = new ValidatedReceipt()
                    {
                        BundleId = receipt.BundleId,
                        ProductId = purchase.ProductId,
                        TransactionId = receipt.TransactionId,
                        OriginalTransactionId = purchase.OrderId,
                        PurchaseDateUtc = DateTime.UnixEpoch.AddMilliseconds(purchase.PurchaseTimeMillis.Value),
                        ServerUtc = DateTime.UtcNow,
                        ExpiryUtc = (DateTime?)null,
                        Token = receipt.Token,
                        DeveloperPayload = receipt.DeveloperPayload,
                    };
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

                if (purchase == null)
                {
                    result = new ValidationResult(false, $"no purchase found");
                }
                else if (!purchase.OrderId.StartsWith(receipt.TransactionId))
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{purchase.OrderId}'");
                }
                else if (purchase.DeveloperPayload != receipt.DeveloperPayload)
                {
                    result = new ValidationResult(false, "DeveloperPayload did not match");
                }                
                else
                {
                    result = new ValidationResult(true);
                    result.ValidatedReceipt = new ValidatedReceipt()
                    {
                        BundleId = receipt.BundleId,
                        ProductId = receipt.ProductId,
                        TransactionId = purchase.OrderId,
                        OriginalTransactionId = receipt.TransactionId,
                        PurchaseDateUtc = DateTime.UnixEpoch.AddMilliseconds(purchase.StartTimeMillis.Value),
                        ExpiryUtc = DateTime.UnixEpoch.AddMilliseconds(purchase.ExpiryTimeMillis.Value),
                        ServerUtc = DateTime.UtcNow,
                        IsExpired = DateTime.UnixEpoch
                                            .AddMilliseconds(purchase.ExpiryTimeMillis.Value)
                                            .AddDays(GraceDays).Date <= DateTime.UtcNow.Date,
                        Token = receipt.Token,
                        DeveloperPayload = purchase.DeveloperPayload,
                    };
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
