using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Iap.Verify
{
    public class AppleVerifyReceipt : Apple
    {
        private const string AppleProductionUrl = "https://buy.itunes.apple.com/verifyReceipt";
        private const string AppleTestUrl = "https://sandbox.itunes.apple.com/verifyReceipt";
        private const string ValidatorRoute = "v1/Apple";

        private readonly ILogger _logger;

        private readonly AppleSecretOptions _secretOptions;

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly int _graceDays;

        public AppleVerifyReceipt(
            IOptions<IapOptions> iapOptions,
            IOptions<AppleSecretOptions> secretOptions,
            IHttpClientFactory httpClientFactory,
            IVerificationRepository verificationRepository,
            ILoggerFactory loggerFactory) : base(verificationRepository)
        {
            _logger = loggerFactory.CreateLogger<AppleVerifyReceipt>();

            _graceDays = iapOptions.Value.GraceDays;
            _secretOptions = secretOptions.Value;

            _httpClient = httpClientFactory.CreateClient();

            _jsonSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            });
        }

        [Function(nameof(AppleVerifyReceipt))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ValidatorRoute)]  HttpRequest req,
            [FromBody] Receipt receipt,
            CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            if (receipt?.IsValid() == true)
            {
                var appleResponse = await PostAppleReceiptAsync(AppleProductionUrl, receipt, _logger, cancellationToken);
                // Apple recommends calling production, then falling back to sandbox on an error code
                if (appleResponse?.WrongEnvironment == true)
                {
                    _logger.LogInformation("Sandbox purchase, calling test environment...");
                    appleResponse = await PostAppleReceiptAsync(AppleTestUrl, receipt, _logger, cancellationToken);
                }

                if (appleResponse?.IsValid == true)
                {
                    result = ValidateProduct(receipt, appleResponse, _logger);
                }
                else if (!string.IsNullOrEmpty(appleResponse?.Error))
                {
                    result = new ValidationResult(false, appleResponse.Error);
                }
                else
                {
                    result = new ValidationResult(false, $"Invalid {nameof(Receipt)}");
                }
            }
            else
            {
                result = new ValidationResult(false, $"Invalid {nameof(Receipt)}");
            }

            return await LogVerificationResultAsync(ValidatorRoute, receipt, result, _logger, cancellationToken)
                ? new JsonResult(result.ValidatedReceipt)
                : new BadRequestResult();
        }

        private async Task<AppleResponse> PostAppleReceiptAsync(string url, Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var appleResponse = default(AppleResponse);

            _secretOptions.Secrets.TryGetValue(receipt.BundleId, out var appSecret);
            if (string.IsNullOrEmpty(appSecret))
                appSecret = _secretOptions.Master;

            if (!string.IsNullOrEmpty(appSecret))
            {
                try
                {
                    var request = new AppleRequest()
                    {
                        ReceiptData = receipt.Token,
                        Password = appSecret
                    };

                    var postBody = new StringContent(JsonSerializer.Serialize(request));
                    using var response = await _httpClient.PostAsync(url, postBody, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    // Expects an iOS 7 style receipt
                    appleResponse = await JsonSerializer.DeserializeAsync<AppleResponse>(stream, options: _jsonSerializerOptions, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to parse AppleResponse: {Message}", ex.Message);
                }
            }

            return appleResponse;
        }

        private ValidationResult ValidateProduct(Receipt receipt, AppleResponse appleResponse, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                receipt.Environment = string.Equals(appleResponse.Environment, Production, StringComparison.OrdinalIgnoreCase)
                    ? EnvironmentType.Production
                    : EnvironmentType.Test;

                if (appleResponse.Receipt is null)
                {
                    result = new ValidationResult(false, "no receipt returned");
                }
                else if (!string.Equals(appleResponse.Receipt.BundleId, receipt.BundleId, StringComparison.Ordinal))
                {
                    result = new ValidationResult(false, $"bundle id '{receipt.BundleId}' does not match '{appleResponse.Receipt.BundleId}'");
                }
                else
                {
                    var purchases = appleResponse.LatestReceiptInfo?.Any() == true
                        ? appleResponse.LatestReceiptInfo.OfType<IAppleInApp>()
                        : appleResponse.Receipt?.InApp?.OfType<IAppleInApp>();
                    var purchase = purchases
                        ?.Where(p => p.ProductId == receipt.ProductId)
                        ?.OrderByDescending(p => long.TryParse(p.PurchaseDateMs, out var ms) ? ms : long.MaxValue)
                        ?.FirstOrDefault();

                    if (purchase is null)
                    {
                        result = new ValidationResult(false, $"did not find '{receipt.ProductId}' in list of purchases");
                    }
                    else
                    {
                        var utcNow = DateTime.UtcNow;

                        var purchaseDateUtc = purchase.GetPurchaseDateUtc();
                        var expiresDateUtc = purchase.GetExpiresDateUtc();
                        var cancellationDateUtc = purchase.GetCancellationDateUtc();
                        var graceDays = _graceDays;

                        var msg = string.Empty;

                        if (cancellationDateUtc.HasValue)
                        {
                            msg = "App Store refunded a transaction or revoked it from family sharing";
                            expiresDateUtc = cancellationDateUtc;
                            graceDays = 0;
                        }

                        result = new ValidationResult(true, msg)
                        {
                            ValidatedReceipt = new ValidatedReceipt()
                            {
                                BundleId = receipt.BundleId,
                                ProductId = receipt.ProductId,
                                TransactionId = purchase.TransactionId,
                                OriginalTransactionId = purchase.OriginalTransactionId,
                                PurchaseDateUtc = purchaseDateUtc,
                                ExpiryUtc = expiresDateUtc,
                                ServerUtc = utcNow,
                                GraceDays = expiresDateUtc.HasValue
                                            ? graceDays
                                            : null,
                                IsExpired = expiresDateUtc.HasValue  &&
                                            expiresDateUtc.Value.AddDays(graceDays) <= utcNow,
                                IsSuspended = false,
                                Token = receipt.Token
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to validate product: {Message}", ex.Message);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }
    }
}
