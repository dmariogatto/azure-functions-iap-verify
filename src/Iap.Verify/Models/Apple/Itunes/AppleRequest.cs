using System.Text.Json.Serialization;

namespace Iap.Verify.Models
{
    public class AppleRequest
    {
        [JsonPropertyName("receipt-data")]
        public string ReceiptData { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}
