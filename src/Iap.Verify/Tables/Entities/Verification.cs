using Iap.Verify.Models;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Globalization;

namespace Iap.Verify.Tables.Entities
{
    public class Verification : TableEntity
    {
        public Verification() { }

        public Verification(Receipt receipt, ValidationResult validationResult)
        {
            if (string.IsNullOrEmpty(receipt?.ProductId))
                throw new ArgumentOutOfRangeException(nameof(receipt.ProductId));
            if (validationResult == null)
                throw new ArgumentNullException(nameof(validationResult));

            PartitionKey = receipt.ProductId;
            RowKey = Convert.ToInt64((DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds)
                .ToString(CultureInfo.InvariantCulture);

            BundleId = receipt.BundleId ?? string.Empty;
            TransactionId = receipt.TransactionId ?? string.Empty;
            DeveloperPayload = receipt.DeveloperPayload ?? string.Empty;
            Token = receipt.Token ?? string.Empty;
            IsValid = validationResult.IsValid;
            Message = validationResult.Message ?? string.Empty;
            Environment = receipt.Environment ?? string.Empty;
            AppVersion = receipt.AppVersion ?? string.Empty;
        }

        public string ProductId { get => PartitionKey; }

        private long _epochMs = 0;
        public DateTime DateVerified
        {
            get
            {
                if (_epochMs > 0 || long.TryParse(RowKey, out _epochMs))
                {
                    return DateTime.UnixEpoch.AddMilliseconds(_epochMs);
                }

                return DateTime.MinValue;
            }
        }

        public string BundleId { get; set; }
        public string TransactionId { get; set; }
        public string DeveloperPayload { get; set; }
        public string Token { get; set; }

        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string Environment { get; set; }
        public string AppVersion { get; set; }
    }
}
