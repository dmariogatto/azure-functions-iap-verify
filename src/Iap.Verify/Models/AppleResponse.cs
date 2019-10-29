using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Iap.Verify.Models
{
    /// <summary>
    /// See, https://developer.apple.com/library/archive/releasenotes/General/ValidateAppStoreReceipt/Chapters/ValidateRemotely.html
    /// </summary>
    public class AppleResponse
    {
        /// <summary>
        /// Either 0 if the receipt is valid, or an error code
        /// </summary>
        public int Status { get; set; }

        public string Environment { get; set; }
        
        /// <summary>
        /// A JSON representation of the receipt that was sent for verification. For information about keys found in a receipt.
        /// </summary>
        public JObject Receipt { get; set; }

        /// <summary>
        /// Only returned for receipts containing auto-renewable subscriptions. Base-64 encoded receipt for the most recent renewal.
        /// </summary>        
        public string LatestReceipt { get; set; }

        /// <summary>
        /// Only returned for receipts containing auto-renewable subscriptions. JSON representation of the receipt for the most recent renewal.
        /// </summary>
        public JArray LatestReceiptInfo { get; set; }

        public bool IsValid => Status == 0;
        public bool WrongEnvironment => Status == 21007 || Status == 21008;

        public string Error
        {
            get
            {
                switch (Status)
                {
                    case 21000: return "The App Store could not read the JSON object you provided.";
                    case 21002: return "The data in the receipt-data property was malformed or missing.";
                    case 21003: return "The receipt could not be authenticated.";
                    case 21004: return "The shared secret you provided does not match the shared secret on file for your account.";
                    case 21005: return "The receipt server is not currently available.";
                    case 21006: return "This receipt is valid but the subscription has expired. When this status code is returned to your server, the receipt data is also decoded and returned as part of the response. Only returned for iOS 6 style transaction receipts for auto-renewable subscriptions.";
                    case 21007: return "This receipt is from the test environment, but it was sent to the production environment for verification. Send it to the test environment instead.";
                    case 21008: return "This receipt is from the production environment, but it was sent to the test environment for verification. Send it to the production environment instead.";
                    case 21010: return "This receipt could not be authorized. Treat this the same as if a purchase was never made.";
                    default:
                        if (Status >= 21100 && Status <= 21199)
                        {
                            return "Internal data access error.";
                        }
                        break;
                }

                return string.Empty;
            }
        }
    }
}
