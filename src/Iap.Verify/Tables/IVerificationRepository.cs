using Iap.Verify.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify.Tables
{
    public interface IVerificationRepository
    {
        Task<bool> SaveLogAsync(string tableName, string validatorName, Receipt receipt, ValidationResult validationResult, CancellationToken cancellationToken);
    }
}
