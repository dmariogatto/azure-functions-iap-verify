using System;
using System.Collections.Generic;
using System.Text;

namespace Iap.Verify.Models
{
    public class AppleReceipt
    {
        public string AppBundleId { get; set; }
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public string Token { get; set; }
    }
}
