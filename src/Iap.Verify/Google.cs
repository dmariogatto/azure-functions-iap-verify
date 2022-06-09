using Google.Apis.AndroidPublisher.v3;
using Google.Apis.AndroidPublisher.v3.Data;
using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify
{
    public class Google
    {
        // https://developers.google.com/android-publisher/api-ref/rest
        private static AndroidPublisherService _googleService;

        private readonly IVerificationRepository _verificationRepository;
        private readonly int _graceDays;

        public Google(
            AndroidPublisherService googleService,
            IVerificationRepository verificationRepository,
            IConfiguration configuration)
        {
            _googleService = googleService;
            _verificationRepository = verificationRepository;

            int.TryParse(configuration["GraceDays"], out _graceDays);
        }

        [FunctionName(nameof(Google))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] Receipt receipt,
            HttpRequest req,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            if (receipt?.IsValid() == true)
            {
                var iapTask = GetInAppProductAsync(receipt.BundleId, receipt.ProductId, cancellationToken);
                var subTask = GetSubscriptionAsync(receipt.BundleId, receipt.ProductId, cancellationToken);
                
                if (await iapTask is not null)
                {
                    // Support legacy subscriptions
                    result = string.Equals(iapTask.Result.PurchaseType, "subscription", StringComparison.OrdinalIgnoreCase)
                        ? await ValidateSubscriptionAsync(receipt, log, cancellationToken)
                        : await ValidateProductAsync(receipt, log, cancellationToken);
                }
                else if (await subTask is not null)
                {
                    result = await ValidateSubscriptionAsync(receipt, log, cancellationToken);
                }
                else
                {
                    result = new ValidationResult(false, $"IAP '{receipt.BundleId}':'{receipt.ProductId}' not found");
                }
            }
            else
            {
                result = new ValidationResult(false, $"Invalid {nameof(Receipt)}");
            }

            await _verificationRepository.SaveLogAsync(nameof(Google), receipt, result, cancellationToken);

            if (result.IsValid && result.ValidatedReceipt is not null)
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

        private async Task<ValidationResult> ValidateProductAsync(Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Products.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchase = await request.ExecuteAsync(cancellationToken);

                receipt.Environment = purchase is null
                    ? EnvironmentType.Unknown
                    : purchase.PurchaseType == 0 ? EnvironmentType.Test : EnvironmentType.Production;

                if (purchase is null)
                {
                    result = new ValidationResult(false, $"no purchase found");
                }
                else if (!purchase.OrderId.StartsWith(receipt.TransactionId, StringComparison.Ordinal))
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{purchase.OrderId}'");
                }
                else if (purchase.PurchaseState != 0)
                {
                    result = new ValidationResult(false, "purchase was cancelled or refunded");
                }
                else
                {
                    result = new ValidationResult(true)
                    {
                        ValidatedReceipt = new ValidatedReceipt()
                        {
                            BundleId = receipt.BundleId,
                            ProductId = purchase.ProductId,
                            TransactionId = receipt.TransactionId,
                            OriginalTransactionId = purchase.OrderId,
                            PurchaseDateUtc = DateTime.UnixEpoch.AddMilliseconds(purchase.PurchaseTimeMillis.Value),
                            ServerUtc = DateTime.UtcNow,
                            Token = receipt.Token
                        }
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

        private async Task<ValidationResult> ValidateSubscriptionAsync(Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Subscriptionsv2.Get(receipt.BundleId, receipt.Token);
                var purchase = await request.ExecuteAsync(cancellationToken);

                receipt.Environment = purchase is null
                    ? EnvironmentType.Unknown
                    : purchase.TestPurchase is not null ? EnvironmentType.Test : EnvironmentType.Production;

                if (purchase is null)
                {
                    result = new ValidationResult(false, $"no purchase found");
                }
                else if (!purchase.LatestOrderId.StartsWith(receipt.TransactionId, StringComparison.Ordinal))
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{purchase.LatestOrderId}'");
                }
                else
                {
                    var utcNow = DateTime.UtcNow;

                    var startTimeUtc = purchase.StartTime as DateTime? ?? DateTime.UnixEpoch;
                    // If the order has been cancelled, then expiry time will set to the cancel date
                    var expiryTimeUtc = purchase.LineItems
                        ?.Select(i => i.ExpiryTime as DateTime?)
                        ?.Where(i => i.HasValue)
                        ?.OrderByDescending(i => i)
                        ?.FirstOrDefault();

                    var suspended = false;
                    var graceDays = _graceDays;

                    // Invalid states
                    switch (purchase.SubscriptionState)
                    {
                        case "SUBSCRIPTION_STATE_PENDING":
                        case "SUBSCRIPTION_STATE_PAUSED":
                        case "SUBSCRIPTION_STATE_ON_HOLD":
                            suspended = true;
                            break;
                        case "SUBSCRIPTION_STATE_CANCELED":
                            graceDays = 0;
                            break;
                    }

                    result = new ValidationResult(true, purchase.SubscriptionState)
                    {
                        ValidatedReceipt = new ValidatedReceipt()
                        {
                            BundleId = receipt.BundleId,
                            ProductId = receipt.ProductId,
                            TransactionId = purchase.LatestOrderId,
                            OriginalTransactionId = receipt.TransactionId,
                            PurchaseDateUtc = startTimeUtc,
                            ExpiryUtc = expiryTimeUtc,
                            ServerUtc = utcNow,
                            GraceDays = expiryTimeUtc.HasValue
                                        ? graceDays
                                        : null,
                            IsExpired = expiryTimeUtc.HasValue &&
                                        expiryTimeUtc.Value.AddDays(graceDays).Date <= utcNow.Date,
                            IsSuspended = suspended,
                            Token = receipt.Token
                        }
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

        private async Task<InAppProduct> GetInAppProductAsync(string bundleId, string productId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(productId))
                return null;

            var result = default(InAppProduct);

            try
            {
                result = await _googleService
                    .Inappproducts
                    .Get(bundleId, productId)
                    .ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            return result;
        }

        private async Task<Subscription> GetSubscriptionAsync(string bundleId, string productId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bundleId) || string.IsNullOrEmpty(productId))
                return null;

            var result = default(Subscription);

            try
            {
                result = await _googleService
                    .Monetization
                    .Subscriptions
                    .Get(bundleId, productId)
                    .ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            return result;
        }
    }
}
