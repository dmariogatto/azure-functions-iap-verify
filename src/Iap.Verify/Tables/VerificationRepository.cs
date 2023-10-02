using Azure.Data.Tables;
using Iap.Verify.Models;
using Iap.Verify.Tables.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Iap.Verify.Tables
{
    public class VerificationRepository : IVerificationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public VerificationRepository(
            TableStorageOptions options,
            ILogger<VerificationRepository> logger)
        {
            if (string.IsNullOrEmpty(options?.AzureWebJobsStorage))
                throw new ArgumentException(nameof(TableStorageOptions.AzureWebJobsStorage));

            _connectionString = options.AzureWebJobsStorage;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SaveLogAsync(string tableName, string validatorName, Receipt receipt, ValidationResult validationResult, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var success = false;

            try
            {
                var tableClient = new TableClient(_connectionString, tableName) ?? throw new NullReferenceException($"Reference to table '{tableName}' cannot be null!");
                var entity = new Verification(receipt, validationResult, validatorName);
                await tableClient.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
                var resp = await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
                success = resp.Status >= 200 && resp.Status <= 299;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save log: {ex.Message}", ex);
            }

            return success;
        }
    }
}
