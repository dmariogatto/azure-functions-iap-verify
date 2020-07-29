using Google.Apis.AndroidPublisher.v3;
using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify
{
    public class Google
    {
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

            _graceDays = int.TryParse(configuration["GraceDays"], out var val)
                ? val : 0;
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
                try
                {
                    var product = await _googleService.Inappproducts.Get(receipt.BundleId, receipt.ProductId)
                        .ExecuteAsync(cancellationToken);

                    if (product != null)
                    {
                        result = product.PurchaseType == "subscription"
                            ? await ValidateSubscriptionAsync(receipt, log, cancellationToken)
                            : await ValidateProductAsync(receipt, log, cancellationToken);
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

            await _verificationRepository.SaveLogAsync(nameof(Google), receipt, result, cancellationToken);

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

        private async Task<ValidationResult> ValidateProductAsync(Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Products.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchase = await request.ExecuteAsync(cancellationToken);

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
                else if (!string.IsNullOrEmpty(receipt.DeveloperPayload) && purchase.DeveloperPayload != receipt.DeveloperPayload)
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

        private async Task<ValidationResult> ValidateSubscriptionAsync(Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            try
            {
                var request = _googleService.Purchases.Subscriptions.Get(receipt.BundleId, receipt.ProductId, receipt.Token);
                var purchase = await request.ExecuteAsync(cancellationToken);

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
                        ExpiryUtc = purchase.ExpiryTimeMillis > 0
                                    ? DateTime.UnixEpoch.AddMilliseconds(purchase.ExpiryTimeMillis.Value)
                                    : (DateTime?)null,
                        ServerUtc = DateTime.UtcNow,
                        IsExpired = purchase.ExpiryTimeMillis > 0
                                    ? DateTime.UnixEpoch
                                              .AddMilliseconds(purchase.ExpiryTimeMillis.Value)
                                              .AddDays(_graceDays).Date <= DateTime.UtcNow.Date
                                    : false,
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
