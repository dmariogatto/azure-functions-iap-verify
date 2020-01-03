using System.Collections.Generic;

namespace Iap.Verify.Models
{
    public class AppleReceipt
    {
        public string BundleId { get; set; }
        public List<ApplePurchase> InApp { get; set; }
    }
}
