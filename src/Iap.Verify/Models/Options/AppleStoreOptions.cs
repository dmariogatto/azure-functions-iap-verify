using System.Text;

namespace Iap.Verify.Models
{
    public class AppleStoreOptions
    {
        public const string AppleStoreKey = "AppleStore";

        public string IssuerId { get; set; }
        public string KeyId { get; set; }

        private string _privateKeyBase64;
        public string PrivateKeyBase64
        {
            get => _privateKeyBase64;
            set
            {
                if (_privateKeyBase64 == value)
                    return;

                _privateKeyBase64 = value;

                if (!string.IsNullOrEmpty(_privateKeyBase64))
                {
                    var base64EncodedBytes = Convert.FromBase64String(_privateKeyBase64);
                    PrivateKey = Encoding.UTF8.GetString(base64EncodedBytes)
                        .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
                        .Replace("-----END PRIVATE KEY-----", string.Empty);
                }
                else
                {
                    PrivateKey = null;
                }
            }
        }

        public string PrivateKey { get; private set; }
    }
}
