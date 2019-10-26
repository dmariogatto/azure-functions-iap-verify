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
        private static HttpClient _httpClient = new HttpClient();
        private static JsonSerializer _serializer = new JsonSerializer()
        {
            ContractResolver = new DefaultContractResolver() { NamingStrategy = new SnakeCaseNamingStrategy() }
        };

        [FunctionName(nameof(Apple))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var receipt = default(AppleReceipt);

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                receipt = JsonConvert.DeserializeObject<AppleReceipt>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Failed to parse {nameof(AppleReceipt)}");
            }
                       
            if (string.IsNullOrEmpty(receipt?.AppBundleId) ||
                string.IsNullOrEmpty(receipt?.ProductId) ||
                string.IsNullOrEmpty(receipt?.TransactionId) ||
                string.IsNullOrEmpty(receipt?.Token))
            {
                return new BadRequestResult();
            }

            log.LogInformation($"IAP receipt: {receipt.ProductId}, {receipt.TransactionId}");

            var result = await PostAppleReceipt(AppleProductionUrl, receipt, log);
            // Apple recommends calling production, then falling back to sandbox on an error code
            if (result?.WrongEnvironment == true)
            {
                log.LogInformation("Sandbox purchase, calling test environment...");
                result = await PostAppleReceipt(AppleTestUrl, receipt, log);
            }

            if (result?.IsValid == true)
            {
                return result.IsAutoRenew
                    ? ValidateIapAutoRenew(receipt, result, log)
                    : ValidateIap(receipt, result, log);
            }

            return new BadRequestResult();
        }

        private static async Task<AppleResponse> PostAppleReceipt(string url, AppleReceipt receipt, ILogger log)
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
                log.LogError(ex, $"Failed to parse {nameof(AppleResponse)}");
            }

            return appleResponse;
        }

        private static IActionResult ValidateIap(AppleReceipt receipt, AppleResponse result, ILogger log)
        {
            if (result.Receipt == null)
            {
                log.LogInformation("IAP invalid, no receipt returned!");
                return new BadRequestResult();
            }

            var bundleId = result.Receipt.Property("bundle_id").Value.Value<string>();
            if (receipt.AppBundleId != bundleId)
            {
                log.LogInformation($"IAP invalid, bundle id '{bundleId ?? string.Empty}' does not match {receipt.AppBundleId}!");
                return new BadRequestResult();
            }

            var purchases = result.Receipt.Property("in_app").Value.Value<JArray>();
            if (purchases == null || purchases.Count == 0)
            {
                log.LogInformation("IAP invalid, no purchases returned!");
                return new BadRequestResult();
            }

            var purchase = purchases.OfType<JObject>().FirstOrDefault(p => p.Property("product_id").Value.Value<string>() == receipt.ProductId);
            if (purchase == null)
            {
                log.LogInformation($"IAP invalid, did not find {receipt.ProductId} in list of purchases!");
                return new BadRequestResult();
            }

            var transactionId = purchase.Property("transaction_id").Value.Value<string>();
            if (receipt.TransactionId != transactionId)
            {
                log.LogInformation($"IAP invalid, transaction id '{transactionId ?? string.Empty}' does not match {receipt.TransactionId}!");
                return new BadRequestResult();
            }

            log.LogInformation($"IAP Success: {receipt.ProductId}, {receipt.TransactionId}");
            return new OkResult();
        }

        private static IActionResult ValidateIapAutoRenew(AppleReceipt receipt, AppleResponse result, ILogger log)
        {
            if (result.Receipt == null || result.LatestReceiptInfo == null)
            {
                log.LogInformation("IAP AutoRenew invalid, no receipt returned!");
                return new BadRequestResult();
            }

            var bundleId = result.LatestReceiptInfo.Property("bid").Value.Value<string>();
            if (receipt.AppBundleId != bundleId)
            {
                log.LogInformation($"IAP AutoRenew invalid, bundle id '{bundleId ?? string.Empty}' does not match {receipt.AppBundleId}!");
                return new BadRequestResult();
            }
            
            var productId = result.LatestReceiptInfo.Property("product_id").Value.Value<string>();
            if (receipt.ProductId != productId)
            {
                log.LogInformation($"IAP AutoRenew invalid, product id '{productId ?? string.Empty}' does not match {receipt.ProductId}!");
                return new BadRequestResult();
            }

            var transactionId = result.Receipt.Property("transaction_id").Value.Value<string>();
            if (receipt.TransactionId != transactionId)
            {
                log.LogInformation($"IAP AutoRenew invalid, transaction id '{transactionId ?? string.Empty}' does not match {receipt.TransactionId}!");
                return new BadRequestResult();
            }

            log.LogInformation($"IAP AutoRenew Success: {receipt.ProductId}, {receipt.TransactionId}");
            return new OkResult();
        }
    }
}
