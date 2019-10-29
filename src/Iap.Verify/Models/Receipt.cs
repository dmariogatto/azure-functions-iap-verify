using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Iap.Verify.Models
{
    public class Receipt
    {
        public string BundleId { get; set; }
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public string DeveloperPayload { get; set; }
        public string Token { get; set; }

        [JsonIgnore]
        public string Environment { get; set; }
    }
}
