using Iap.Verify.Models;
using Iap.Verify.Tables.Entities;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify.Tables
{
    public class VerificationRepository : IVerificationRepository
    {
        private readonly CloudTableClient _cloudTableClient;

        private ILogger _logger;

        public VerificationRepository(
            CloudTableClient tableClient,
            ILogger<VerificationRepository> logger)
        {
            _cloudTableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
           _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SaveLogAsync(string tableName, Receipt receipt, ValidationResult validationResult, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var result = false;

            try
            {
                var cloudTable = _cloudTableClient.GetTableReference(tableName) ?? throw new NullReferenceException($"Reference to table '{tableName}' cannot be null!");
                var entity = new Verification(receipt, validationResult);
                await cloudTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);                
                var insertOp = TableOperation.Insert(entity);
                var exeResult = await cloudTable.ExecuteAsync(insertOp, cancellationToken).ConfigureAwait(false);
                result = exeResult.HttpStatusCode >= 200 && exeResult.HttpStatusCode <= 299;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save log: {ex.Message}", ex);
            }

            return result;
        }
    }
}
