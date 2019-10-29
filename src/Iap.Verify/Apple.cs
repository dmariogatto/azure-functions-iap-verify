using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Iap.Verify.Models;
using Newtonsoft.Json.Linq;
using System.Linq;
using Newtonsoft.Json.Serialization;

namespace Iap.Verify
{
    public static class Apple
    {
        private const string AppleProductionUrl = "https://buy.itunes.apple.com/verifyReceipt";
        private const string AppleTestUrl = "https://sandbox.itunes.apple.com/verifyReceipt";
        private static readonly  HttpClient _httpClient = new HttpClient();
        private static readonly JsonSerializer _serializer = new JsonSerializer()
        {
            ContractResolver = new DefaultContractResolver() { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        [FunctionName(nameof(Apple))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
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
                log.LogError(ex, $"Failed to parse {nameof(Receipt)}");
            }
                       
            if (!string.IsNullOrEmpty(receipt?.BundleId) &&
                !string.IsNullOrEmpty(receipt?.ProductId) &&
                !string.IsNullOrEmpty(receipt?.TransactionId) &&
                !string.IsNullOrEmpty(receipt?.Token))
            {
                var appleResponse = await PostAppleReceipt(AppleProductionUrl, receipt, log);
                // Apple recommends calling production, then falling back to sandbox on an error code
                if (appleResponse?.WrongEnvironment == true)
                {
                    log.LogInformation("Sandbox purchase, calling test environment...");
                    appleResponse = await PostAppleReceipt(AppleTestUrl, receipt, log);
                }

                if (appleResponse?.IsValid == true)
                {
                    result = appleResponse.IsAutoRenew
                        ? ValidateSubscription(receipt, appleResponse, log)
                        : ValidateProduct(receipt, appleResponse, log);
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

        private static async Task<AppleResponse> PostAppleReceipt(string url, Receipt receipt, ILogger log)
        {
            var appleResponse = default(AppleResponse);

            try
            {
                var json = new JObject(
                    new JProperty("receipt-data", receipt.Token),
                    new JProperty("password", Secrets.Apple)).ToString();
                var response = await _httpClient.PostAsync(url, new StringContent(json));
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
                log.LogError($"Failed to parse {nameof(AppleResponse)}", ex);
            }

            return appleResponse;
        }

        private static ValidationResult ValidateProduct(Receipt receipt, AppleResponse appleResponse, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                if (appleResponse.Receipt == null)
                {
                    result = new ValidationResult(false, "no receipt returned");
                }
                else if (appleResponse.Receipt.Property("bundle_id").Value.Value<string>() is string bid &&
                         receipt.BundleId != bid)
                {
                    result = new ValidationResult(false, $"bundle id '{receipt.BundleId}' does not match '{bid}'");
                }
                else
                {
                    var purchases = appleResponse.Receipt.Property("in_app").Value.Value<JArray>();
                    var purchase = purchases?.Count > 0
                        ? purchases.OfType<JObject>().FirstOrDefault(p => p.Property("product_id").Value.Value<string>() == receipt.ProductId)
                        : default;

                    if (purchase == default)
                    {
                        result = new ValidationResult(false, $"did not find '{receipt.ProductId}' in list of purchases");
                    }
                    else if (purchase.Property("transaction_id").Value.Value<string>() is string transId &&
                             receipt.TransactionId != transId)
                    {
                        result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{transId}'");
                    }
                    else
                    {
                        result = new ValidationResult(true);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError("Failed to validate product", ex);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }

        private static ValidationResult ValidateSubscription(Receipt receipt, AppleResponse appleResponse, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                if (appleResponse.Receipt == null)
                {
                    result = new ValidationResult(false, "no receipt returned");
                }
                else if (appleResponse.Receipt.Property("bid").Value.Value<string>() is string bid &&
                         receipt.BundleId != bid)
                {
                    result = new ValidationResult(false, $"bundle id '{receipt.BundleId}' does not match '{bid}'");
                }
                else if (appleResponse.Receipt.Property("product_id").Value.Value<string>() is string productId &&
                         receipt.ProductId != productId)
                {
                    result = new ValidationResult(false, $"product id '{receipt.ProductId}' does not match '{productId}'");
                }
                else if (appleResponse.Receipt.Property("transaction_id").Value.Value<string>() is string transId &&
                         receipt.TransactionId != transId)
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{transId}'");
                }
                else
                {
                    result = new ValidationResult(true);
                }
            }
            catch (Exception ex)
            {
                log.LogError("Failed to validate subscription", ex);
                result = new ValidationResult(false, ex.Message);
            }

            return result;            
        }
    }
}
