using System;

namespace Iap.Verify.Models
{
    /// <summary>
    /// An array of elements that refers to open or failed auto-renewable subscription renewals.
    /// See, https://developer.apple.com/documentation/appstorereceipts/responsebody/pending_renewal_info?language=objc
    /// </summary>
    public class ApplePendingRenewalInfo
    {
        /// <summary>
        /// The value for this key corresponds to the productIdentifier property of the product that the customer’s subscription renews.
        /// </summary>
        public string AutoRenewProductId { get; set; }

        /// <summary>
        /// The current renewal status for the auto-renewable subscription.
        /// See https://developer.apple.com/documentation/appstorereceipts/auto_renew_status?language=objc
        /// </summary>
        public string AutoRenewStatus { get; set; }

        /// <summary>
        /// The time at which the grace period for subscription renewals expires, in a date-time format similar to the ISO 8601.
        /// </summary>
        public string GracePeroidExpiresDate { get; set; }

        /// <summary>
        /// The time at which the grace period for subscription renewals expires, in UNIX epoch time format, in milliseconds. This key is present only for apps that have Billing Grace Period enabled and when the user experiences a billing error at the time of renewal. Use this time format for processing dates.
        /// </summary>
        public string GracePeroidExpiresDateMs { get; set; }

        /// <summary>
        /// The time at which the grace period for subscription renewals expires, in the Pacific Time zone.
        /// </summary>
        public string GracePeroidExpiresDatePst { get; set; }

        /// <summary>
        /// A flag that indicates Apple is attempting to renew an expired subscription automatically. This field is present only if an auto-renewable subscription is in the billing retry state.
        /// See, https://developer.apple.com/documentation/appstorereceipts/is_in_billing_retry_period?language=objc
        /// </summary>
        public string IsInBillingRetryPeriod { get; set; }

        /// <summary>
        /// The reference name of a subscription offer that you configured in App Store Connect. This field is present when a customer redeemed a subscription offer code.
        /// See, https://developer.apple.com/documentation/appstorereceipts/offer_code_ref_name?language=objc
        /// </summary>
        public string OfferCodeRefName { get; set; }

        /// <summary>
        /// The transaction identifier of the original purchase. 
        /// </summary>
        public string OriginalTransactionId { get; set; }

        /// <summary>
        /// The price consent status for an auto-renewable subscription price increase that requires customer consent. This field is present only if the App Store requested customer consent for a price increase that requires customer consent. The default value is "0" and changes to "1" if the customer consents.
        /// </summary>
        public string PriceConsentStatus { get; set; }

        /// <summary>
        /// The unique identifier of the product purchased. You provide this value when creating the product in App Store Connect, and it corresponds to the productIdentifier property of the SKPayment object stored in the transaction's payment property.
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// The identifier of the promotional offer for an auto-renewable subscription that the user redeemed. You provide this value in the Promotional Offer Identifier field when you create the promotional offer in App Store Connect.
        /// </summary>
        public string PromotionalOfferId { get; set; }

        /// <summary>
        /// The status that indicates if an auto-renewable subscription is subject to a price increase.
        /// 
        /// The price increase status is "0" when the App Store has requested consent for an auto-renewable subscription price increase that requires customer consent, and the customer hasn't yet consented.
        /// 
        /// The price increase status is "1" if the customer has consented to a price increase that requires customer consent.
        /// 
        /// The price increase status is also "1" if the App Store has notified the customer of the auto-renewable subscription price increase that doesn't require customer consent.
        /// </summary>
        public string PriceIncreaseStatus { get; set; }
    }
}
