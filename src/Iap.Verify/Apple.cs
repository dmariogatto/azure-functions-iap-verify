using Iap.Verify.Models;
using Iap.Verify.Tables;
using Microsoft.Extensions.Logging;

namespace Iap.Verify
{
    public abstract class Apple
    {
        protected const string Production = nameof(Production);
        protected const string GraceDays = nameof(GraceDays);

        private readonly IVerificationRepository _verificationRepository;

        public Apple(
            IVerificationRepository verificationRepository)
        {
            _verificationRepository = verificationRepository;
        }

        protected async Task<bool> LogVerificationResultAsync(string validatorName, Receipt receipt, ValidationResult result, ILogger log, CancellationToken cancellationToken)
        {
            await _verificationRepository.SaveLogAsync(nameof(Apple), validatorName, receipt, result, cancellationToken);

            if (result.IsValid && result.ValidatedReceipt is not null)
            {
                log.LogInformation("Validated IAP '{BundleId}':'{ProductId}'", receipt.BundleId, receipt.ProductId);
                return true;
            }

            if (!string.IsNullOrEmpty(receipt?.BundleId) &&
                !string.IsNullOrEmpty(receipt?.ProductId))
            {
                log.LogInformation("Failed to validate IAP '{BundleId}':'{ProductId}', reason '{Message}'", receipt.BundleId, receipt.ProductId, result?.Message ?? string.Empty);
            }
            else
            {
                log.LogInformation("Failed to validate IAP, reason '{Message}'", result?.Message ?? string.Empty);
            }

            return false;
        }
    }
}
