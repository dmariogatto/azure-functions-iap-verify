using Iap.Verify.Models;
using Iap.Verify.Tables.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Threading.Tasks;

namespace Iap.Verify.Tables
{
    public static class Storage
    {
        public static async Task SaveLog(CloudTable table, Receipt receipt, ValidationResult validationResult, ILogger log)
        {
            try
            {
                var entity = new Verification(receipt, validationResult);
                await table.CreateIfNotExistsAsync().ConfigureAwait(false);
                var insertOp = TableOperation.Insert(entity);
                await table.ExecuteAsync(insertOp).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to save log: {ex.Message}", ex);
            }
        }
    }
}
