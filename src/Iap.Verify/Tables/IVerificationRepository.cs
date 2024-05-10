using Iap.Verify.Models;

namespace Iap.Verify.Tables
{
    public interface IVerificationRepository
    {
        Task<bool> SaveLogAsync(string tableName, string validatorName, Receipt receipt, ValidationResult validationResult, CancellationToken cancellationToken);
    }
}
