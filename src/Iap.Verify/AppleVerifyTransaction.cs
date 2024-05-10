using Azure.Core;
using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Iap.Verify
{
    public class AppleVerifyTransaction : Apple
    {
        private const string AppleProductionUrl = "https://api.storekit.itunes.apple.com/inApps/v1/transactions/{0}";
        private const string AppleTestUrl = "https://api.storekit-sandbox.itunes.apple.com/inApps/v1/transactions/{0}";
        private const string ValidatorRoute = "v2/Apple";

        private readonly static IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());

        private readonly ILogger _logger;

        private readonly AppleStoreOptions _storeKeyOptions;

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly JwtSecurityTokenHandler _jwtHandler;
        private readonly int _graceDays;

        public AppleVerifyTransaction(
            IOptions<AppleStoreOptions> storeKeyOptions,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IVerificationRepository verificationRepository,
            ILoggerFactory loggerFactory) : base(verificationRepository)
        {
            _logger = loggerFactory.CreateLogger<AppleVerifyTransaction>();

            _storeKeyOptions = storeKeyOptions.Value;

            _httpClient = httpClientFactory.CreateClient();

            _jsonSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            _jwtHandler = new JwtSecurityTokenHandler();

            _ = int.TryParse(configuration[GraceDays], out _graceDays);
        }

        [Function(nameof(AppleVerifyTransaction))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ValidatorRoute)] HttpRequest req,
            [FromBody] Receipt receipt,
            CancellationToken cancellationToken)
        {
            var result = default(ValidationResult);

            if (receipt?.IsValid() == true)
            {
                var url = string.Format(AppleProductionUrl, receipt.TransactionId);
                var jwt = GetCachedAppStoreJwt(receipt.BundleId);

                var storeKitResponse = await GetTransactionAsync(url, jwt, _logger, cancellationToken);
                if (string.IsNullOrEmpty(storeKitResponse?.SignedTransactionInfo))
                {
                    _logger.LogInformation("Attempting sandbox purchase, calling test environment...");
                    url = string.Format(AppleTestUrl, receipt.TransactionId);
                    storeKitResponse = await GetTransactionAsync(url, jwt, _logger, cancellationToken);
                }

                if (!string.IsNullOrEmpty(storeKitResponse?.SignedTransactionInfo))
                {
                    var token = _jwtHandler.ReadJwtToken(storeKitResponse.SignedTransactionInfo);
                    var payload = Base64UrlEncoder.Decode(token.EncodedPayload);
                    var transactionInfo = JsonSerializer.Deserialize<StoreKitTransactionInfo>(payload, options: _jsonSerializerOptions);
                    result = ValidateTransaction(receipt, transactionInfo, _logger);
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

        private async Task<StoreKitTransactionResponse> GetTransactionAsync(string url, string jwt, ILogger log, CancellationToken cancellationToken)
        {
            var storeKitResponse = default(StoreKitTransactionResponse);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                storeKitResponse = await JsonSerializer.DeserializeAsync<StoreKitTransactionResponse>(stream, options: _jsonSerializerOptions, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to parse StoreKitTransactionResponse: {Message}", ex.Message);
            }

            return storeKitResponse;
        }

        private ValidationResult ValidateTransaction(Receipt receipt, StoreKitTransactionInfo transactionInfo, ILogger log)
        {
            var result = default(ValidationResult);

            try
            {
                receipt.Environment = string.Equals(transactionInfo.Environment, Production, StringComparison.OrdinalIgnoreCase)
                    ? EnvironmentType.Production
                    : EnvironmentType.Test;

                if (!string.Equals(transactionInfo.TransactionId, receipt.TransactionId, StringComparison.Ordinal))
                {
                    result = new ValidationResult(false, $"transaction id '{receipt.TransactionId}' does not match '{transactionInfo.TransactionId}'");
                }
                else if (!string.Equals(transactionInfo.BundleId, receipt.BundleId, StringComparison.Ordinal))
                {
                    result = new ValidationResult(false, $"bundle id '{receipt.BundleId}' does not match '{transactionInfo.BundleId}'");
                }
                else
                {
                    var utcNow = DateTime.UtcNow;

                    var purchaseDateUtc = DateTime.UnixEpoch.AddMilliseconds(transactionInfo.PurchaseDate);
                    var expiresDateUtc = transactionInfo.ExpiresDate.HasValue
                        ? DateTime.UnixEpoch.AddMilliseconds(transactionInfo.ExpiresDate.Value)
                        : (DateTime?)null;
                    var cancellationDateUtc = transactionInfo.RevocationDate.HasValue
                        ? DateTime.UnixEpoch.AddMilliseconds(transactionInfo.RevocationDate.Value)
                        : (DateTime?)null;
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
                            TransactionId = transactionInfo.TransactionId,
                            OriginalTransactionId = transactionInfo.OriginalTransactionId,
                            PurchaseDateUtc = purchaseDateUtc,
                            ExpiryUtc = expiresDateUtc,
                            ServerUtc = utcNow,
                            GraceDays = expiresDateUtc.HasValue
                                        ? graceDays
                                        : null,
                            IsExpired = expiresDateUtc.HasValue &&
                                        expiresDateUtc.Value.AddDays(graceDays) <= utcNow,
                            IsSuspended = false,
                            Token = receipt.Token
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to validate transaction: {Message}", ex.Message);
                result = new ValidationResult(false, ex.Message);
            }

            return result;
        }

        private string GetAppStoreJwt(string bundleId)
        {
            var issuedAt = DateTime.UtcNow.AddSeconds(-5);

            var claims = new List<Claim>()
            {
                new Claim("bid", bundleId),
                new Claim("iat", EpochTime.GetIntDate(issuedAt).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            };

            using var algorithm = ECDsa.Create();
            algorithm.ImportPkcs8PrivateKey(Convert.FromBase64String(_storeKeyOptions.PrivateKey), out var _);
            var key = new ECDsaSecurityKey(algorithm) { KeyId = _storeKeyOptions.KeyId };
            var cred = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory() { CacheSignatureProviders = false }
            };

            var token = new JwtSecurityToken(
                _storeKeyOptions.IssuerId,
                "appstoreconnect-v1",
                claims: claims,
                expires: issuedAt.AddMinutes(20),
                signingCredentials: cred);

            var jwt = _jwtHandler.WriteToken(token);
            return jwt;
        }

        private string GetCachedAppStoreJwt(string bundleId)
        {
            var key = $"{nameof(AppleVerifyTransaction)}_{bundleId}";

            if (Cache.TryGetValue<string>(key, out var jwt))
                return jwt;

            jwt = GetAppStoreJwt(bundleId);
            Cache.Set(key, jwt, DateTimeOffset.UtcNow.AddMinutes(15));

            return jwt;
        }
    }
}
