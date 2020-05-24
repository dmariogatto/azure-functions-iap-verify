using System;

namespace Iap.Verify.Models
{
    public class ApplePurchase
    {
        public string ProductId { get; set; }

        public string TransactionId { get; set; }
        public string OriginalTransactionId { get; set; }

        public long PurchaseDateMs { get; set; }
        public long? ExpiresDateMs { get; set; }

        public DateTime PurchaseDateUtc => DateTime.UnixEpoch.AddMilliseconds(PurchaseDateMs);
        public DateTime? ExpiresDateUtc => ExpiresDateMs > 0 ? DateTime.UnixEpoch.AddMilliseconds(ExpiresDateMs.Value) : (DateTime?)null;
    }
}
