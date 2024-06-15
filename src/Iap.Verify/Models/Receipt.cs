using System.Text.Json.Serialization;

namespace Iap.Verify.Models
{
    public class Receipt
    {
        public string BundleId { get; set; }
        public string ProductId { get; set; }
        public string TransactionId { get; set; }
        public string Token { get; set; }

        public string AppVersion { get; set; }

        [JsonIgnore]
        public EnvironmentType Environment { get; set; }

        public bool IsValid() =>
            !string.IsNullOrEmpty(BundleId) &&
            !string.IsNullOrEmpty(ProductId) &&
            !string.IsNullOrEmpty(TransactionId) &&
            !string.IsNullOrEmpty(Token);
    }
}
