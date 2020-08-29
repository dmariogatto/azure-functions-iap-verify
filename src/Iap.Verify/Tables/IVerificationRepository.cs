using Iap.Verify.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify.Tables
{
    public interface IVerificationRepository
    {
        Task<bool> SaveLogAsync(string tableName, Receipt receipt, ValidationResult validationResult, CancellationToken cancellationToken);
    }
}
