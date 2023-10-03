using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify
{
    public class AppleVerifyReceipt : Apple
    {
        private const string AppleProductionUrl = "https://buy.itunes.apple.com/verifyReceipt";
        private const string AppleTestUrl = "https://sandbox.itunes.apple.com/verifyReceipt";
        private const string ValidatorRoute = "v1/Apple";

        private readonly AppleSecretOptions _secretOptions;

        private readonly HttpClient _httpClient;
        private readonly JsonSerializer _serializer;
        private readonly int _graceDays;

        public AppleVerifyReceipt(
            IOptions<AppleSecretOptions> secretOptions,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IVerificationRepository verificationRepository) : base(verificationRepository)
        {
            _secretOptions = secretOptions.Value;

            _httpClient = httpClientFactory.CreateClient();

            _serializer = new JsonSerializer()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

            _ = int.TryParse(configuration[Startup.GraceDaysKey], out _graceDays);
        }

        [FunctionName(nameof(AppleVerifyReceipt))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ValidatorRoute)] Receipt receipt,
            HttpRequest req,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            if (receipt?.IsValid() == true)
            {
                var appleResponse = await PostAppleReceiptAsync(AppleProductionUrl, receipt, log, cancellationToken);
                // Apple recommends calling production, then falling back to sandbox on an error code
                if (appleResponse?.WrongEnvironment == true)
                {
                    log.LogInformation("Sandbox purchase, calling test environment...");
                    appleResponse = await PostAppleReceiptAsync(AppleTestUrl, receipt, log, cancellationToken);
                }

                if (appleResponse?.IsValid == true)
                {
                    result = ValidateProduct(receipt, appleResponse, log);
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

            return await LogVerificationResultAsync(ValidatorRoute, receipt, result, log, cancellationToken)
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

                    var postBody = new StringContent(JsonConvert.SerializeObject(request));
                    using var response = await _httpClient.PostAsync(url, postBody, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var reader = new StreamReader(stream);
                    using var jsonReader = new JsonTextReader(reader);

                    // Expects an iOS 7 style receipt
                    appleResponse = _serializer.Deserialize<AppleResponse>(jsonReader);
                }
                catch (Exception ex)
                {
                    log.LogError($"Failed to parse {nameof(AppleResponse)}: {ex.Message}", ex);
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
                log.LogError($"Failed to validate product: {ex.Message}", ex);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }
    }
}
