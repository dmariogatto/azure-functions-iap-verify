using Azure;
using Azure.Data.Tables;
using System;

namespace Iap.Verify.Tables.Entities
{
    public abstract class BaseTableStoreEntity : ITableEntity
    {
        public BaseTableStoreEntity() { }

        public BaseTableStoreEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
