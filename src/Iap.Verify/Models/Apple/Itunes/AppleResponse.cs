using Newtonsoft.Json;
using System.Collections.Generic;

namespace Iap.Verify.Models
{
    /// <summary>
    /// The data returned in the response from the App Store.
    /// See, https://developer.apple.com/documentation/appstorereceipts/responsebody?language=objc
    /// </summary>
    public class AppleResponse
    {
        /// <summary>
        /// The environment for which the receipt was generated.
        /// Possible values: Sandbox, Production
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// An indicator that an error occurred during the request. A value of 1 indicates a temporary issue; retry validation for this receipt at a later time.
        /// A value of 0 indicates an unresolvable issue; do not retry validation for this receipt. Only applicable to status codes 21100-21199.
        /// </summary>
        [JsonProperty("is-retryable")]
        public bool IsRetryable { get; set; }

        /// <summary>
        /// The latest Base64 encoded app receipt. Only returned for receipts that contain auto-renewable subscriptions.
        /// </summary>
        public string LatestReceipt { get; set; }

        /// <summary>
        /// An array that contains all in-app purchase transactions. This excludes transactions for consumable products that have been marked as finished by your app. Only returned for receipts that contain auto-renewable subscriptions.
        /// </summary>
        public List<AppleLatestReceiptInfo> LatestReceiptInfo { get; set; }

        /// <summary>
        /// An array where each element contains the pending renewal information for each auto-renewable subscription identified by the <em>product_id</em>. Only returned for app receipts that contain auto-renewable subscriptions.
        /// </summary>
        public List<ApplePendingRenewalInfo> PendingRenewalInfo { get; set; }

        /// <summary>
        /// A representation of the receipt that was sent for verification.
        /// </summary>
        public AppleReceipt Receipt { get; set; }

        /// <summary>
        /// Either 0 if the receipt is valid, or a status code if there is an error. The status code reflects the status of the app receipt as a whole.
        /// See, https://developer.apple.com/documentation/appstorereceipts/status?language=objc
        /// </summary>
        public int Status { get; set; }

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
