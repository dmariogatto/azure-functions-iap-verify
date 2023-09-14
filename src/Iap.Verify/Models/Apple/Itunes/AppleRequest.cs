using Newtonsoft.Json;

namespace Iap.Verify.Models
{
    public class AppleRequest
    {
        [JsonProperty("receipt-data")]
        public string ReceiptData { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
}
