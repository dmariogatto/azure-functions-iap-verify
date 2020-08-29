using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify
{
    public class Apple
    {
        private const string AppleProductionUrl = "https://buy.itunes.apple.com/verifyReceipt";
        private const string AppleTestUrl = "https://sandbox.itunes.apple.com/verifyReceipt";

        private readonly IVerificationRepository _verificationRepository;

        private readonly HttpClient _httpClient;
        private readonly JsonSerializer _serializer;
        private readonly IConfiguration _configuration;
        private readonly int _graceDays;

        public Apple(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IVerificationRepository verificationRepository)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _verificationRepository = verificationRepository;

            _serializer = new JsonSerializer()
            {
                ContractResolver = new DefaultContractResolver() { NamingStrategy = new SnakeCaseNamingStrategy() }
            };

            _graceDays = int.TryParse(_configuration["GraceDays"], out var val)
                ? val : 0;
        }

        [FunctionName(nameof(Apple))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] Receipt receipt,
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

            await _verificationRepository.SaveLogAsync(nameof(Apple), receipt, result, cancellationToken);

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

        private async Task<AppleResponse> PostAppleReceiptAsync(string url, Receipt receipt, ILogger log, CancellationToken cancellationToken)
        {
            var appleResponse = default(AppleResponse);

            var appSecret = _configuration[$"AppleSecret.{receipt.BundleId}"];
            if (string.IsNullOrEmpty(appSecret))
                appSecret = _configuration["AppleSecret"];

            if (!string.IsNullOrEmpty(appSecret))
            {
                try
                {
                    var json = new JObject(
                        new JProperty("receipt-data", receipt.Token),
                        new JProperty("password", appSecret)).ToString();
                    var response = await _httpClient.PostAsync(url, new StringContent(json), cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        appleResponse = _serializer.Deserialize<AppleResponse>(jsonReader);
                    }
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
                receipt.Environment = appleResponse.Environment;

                if (appleResponse.Receipt == null)
                {
                    result = new ValidationResult(false, "no receipt returned");
                }
                else if (appleResponse.Receipt.BundleId != receipt.BundleId)
                {
                    result = new ValidationResult(false, $"bundle id '{receipt.BundleId}' does not match '{appleResponse.Receipt.BundleId}'");
                }
                else
                {
                    var purchases = appleResponse.LatestReceiptInfo?.Any() == true
                        ? appleResponse.LatestReceiptInfo
                        : appleResponse.Receipt?.InApp;
                    var purchase = purchases?.Any() == true
                        ? purchases
                            .Where(p => p.ProductId == receipt.ProductId)
                            .OrderBy(p => p.PurchaseDateMs)
                            .LastOrDefault()
                        : null;

                    if (purchase == null)
                    {
                        result = new ValidationResult(false, $"did not find '{receipt.ProductId}' in list of purchases");
                    }
                    else
                    {
                        if (receipt.TransactionId != purchase.TransactionId && receipt.TransactionId != purchase.OriginalTransactionId)
                        {
                            result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match either original '{purchase.OriginalTransactionId}', or '{purchase.TransactionId}'");
                        }
                        else
                        {
                            result = new ValidationResult(true)
                            {
                                ValidatedReceipt = new ValidatedReceipt()
                                {
                                    BundleId = receipt.BundleId,
                                    ProductId = receipt.ProductId,
                                    TransactionId = purchase.TransactionId,
                                    OriginalTransactionId = purchase.OriginalTransactionId,
                                    PurchaseDateUtc = purchase.PurchaseDateUtc,
                                    ExpiryUtc = purchase.ExpiresDateUtc,
                                    ServerUtc = DateTime.UtcNow,
                                    IsExpired = purchase.ExpiresDateMs > 0 &&
                                            DateTime.UnixEpoch
                                                    .AddMilliseconds(purchase.ExpiresDateMs.Value)
                                                    .AddDays(_graceDays).Date <= DateTime.UtcNow.Date,
                                    Token = receipt.Token,
                                    DeveloperPayload = receipt.DeveloperPayload,
                                }
                            };
                        }
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
