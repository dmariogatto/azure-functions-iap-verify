using System;

namespace Iap.Verify.Models
{
    /// <summary>
    /// See, https://developer.apple.com/documentation/appstorereceipts/responsebody/latest_receipt_info?language=objc
    /// </summary>
    public class AppleLatestReceiptInfo : IAppleInApp
    {
        /// <summary>
        /// The <em>appAccountToken</em> associated with this transaction. This field is only present if your app supplied an <em>appAccountToken(_:)</em> or provided a UUID for the <em>applicationUsername</em> property when the user made the purchase.
        /// </summary>
        public string AppAccountToken { get; set; }

        /// <summary>
        /// The time the App Store refunded a transaction or revoked it from family sharing, in a date-time format similar to the ISO 8601. This field is present only for refunded or revoked transactions.
        /// </summary>
        public string CancellationDate { get; set; }

        /// <summary>
        /// The time the App Store refunded a transaction or revoked it from family sharing, in UNIX epoch time format, in milliseconds. This field is present only for refunded or revoked transactions. Use this time format for processing dates.
        /// </summary>
        public string CancellationDateMs { get; set; }

        /// <summary>
        /// The time the App Store refunded a transaction or revoked it from family sharing, in Pacific Standard Time. This field is present only for refunded or revoked transactions.
        /// </summary>
        public string CancellationDatePst { get; set; }

        /// <summary>
        /// The reason for a refunded or revoked transaction. A value of 1 indicates that the customer canceled their transaction due to an actual or perceived issue within your app. A value of 0 indicates that the transaction was canceled for another reason; for example, if the customer made the purchase accidentally.
        /// </summary>
        public string CancellationReason { get; set; }

        /// <summary>
        /// The time a subscription expires or when it will renew, in a date-time format similar to the ISO 8601.
        /// </summary>
        public string ExpiresDate { get; set; }

        /// <summary>
        /// The time a subscription expires or when it will renew, in UNIX epoch time format, in milliseconds. Use this time format for processing dates.
        /// </summary>
        public string ExpiresDateMs { get; set; }

        /// <summary>
        /// The time a subscription expires or when it will renew, in Pacific Standard Time.
        /// </summary>
        public string ExpiresDatePst { get; set; }

        /// <summary>
        /// A value that indicates whether the user is the purchaser of the product or is a family member with access to the product through Family Sharing.
        ///
        /// Possible values,
        /// <strong>FAMILY_SHARED</strong>: The transaction belongs to a family member who benefits from service.
        /// <strong>PURCHASED</strong>: The transaction belongs to the purchaser.
        /// </summary>
        public string InAppOwnershipType { get; set; }

        /// <summary>
        /// An indicator of whether an auto-renewable subscription is in the introductory price period.
        ///
        /// Possible values,
        /// <strong>true</strong>: The customer’s subscription is in an introductory price period
        /// <strong>false</strong>: The subscription is not in an introductory price period.
        /// </summary>
        public string IsInIntroOfferPeriod { get; set; }

        /// <summary>
        /// An indicator of whether an auto-renewable subscription is in the free trial period.
        ///
        /// Possible values,
        /// <strong>true</strong>: The subscription is in the free trial period.
        /// <strong>false</strong>: The subscription is not in the free trial period.
        /// </summary>
        public string IsTrialPeriod { get; set; }

        /// <summary>
        /// An indicator that a subscription has been canceled due to an upgrade. This field is only present for upgrade transactions.
        ///
        /// Possible values,
        /// <strong>true</strong>
        /// </summary>
        public string IsUpgrade { get; set; }

        /// <summary>
        /// The reference name of a subscription offer that you configured in App Store Connect. This field is present when a customer redeemed a subscription offer code.
        /// See, https://developer.apple.com/documentation/appstorereceipts/offer_code_ref_name?language=objc
        /// </summary>
        public string OfferCodeRefName { get; set; }

        /// <summary>
        /// The time of the original app purchase, in a date-time format similar to ISO 8601.
        /// </summary>
        public string OriginalPurchaseDate { get; set; }

        /// <summary>
        /// The time of the original app purchase, in UNIX epoch time format, in milliseconds. Use this time format for processing dates. For an auto-renewable subscription, this value indicates the date of the subscription’s initial purchase. The original purchase date applies to all product types and remains the same in all transactions for the same product ID. This value corresponds to the original transaction’s <em>transactionDate</em> property in StoreKit.
        /// </summary>
        public string OriginalPurchaseDateMs { get; set; }

        /// <summary>
        /// The time of the original app purchase, in Pacific Standard Time.
        /// </summary>
        public string OriginalPurchaseDatePst { get; set; }

        /// <summary>
        /// The transaction identifier of the original purchase.
        /// </summary>
        public string OriginalTransactionId { get; set; }

        /// <summary>
        /// The unique identifier of the product purchased. You provide this value when creating the product in App Store Connect, and it corresponds to the <em>productIdentifier</em> property of the SKPayment object stored in the transaction’s payment property.
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// The identifier of the subscription offer redeemed by the user.
        /// </summary>
        public string PromotionalOfferId { get; set; }

        /// <summary>
        /// The time the App Store charged the user’s account for a purchased or restored product, or the time the App Store charged the user’s account for a subscription purchase or renewal after a lapse, in a date-time format similar to ISO 8601.
        /// </summary>
        public string PurchaseDate { get; set; }

        /// <summary>
        /// For consumable, non-consumable, and non-renewing subscription products, the time the App Store charged the user’s account for a purchased or restored product, in the UNIX epoch time format, in milliseconds. For auto-renewable subscriptions, the time the App Store charged the user’s account for a subscription purchase or renewal after a lapse, in the UNIX epoch time format, in milliseconds. Use this time format for processing dates.
        /// </summary>
        public string PurchaseDateMs { get; set; }

        /// <summary>
        /// The time the App Store charged the user’s account for a purchased or restored product, or the time the App Store charged the user’s account for a subscription purchase or renewal after a lapse, in Pacific Standard Time.
        /// </summary>
        public string PurchaseDatePst { get; set; }

        /// <summary>
        /// The number of consumable products purchased. This value corresponds to the <em>quantity</em> property of the SKPayment object stored in the transaction’s payment property. The value is usually 1 unless modified with a mutable payment. The maximum value is 10.
        /// </summary>
        public string Quantity { get; set; }

        /// <summary>
        /// The identifier of the subscription group to which the subscription belongs. The value for this field is identical to the <em>subscriptionGroupIdentifier</em> property in SKProduct.
        /// </summary>
        public string SubscriptionGroupIdentifier { get; set; }

        /// <summary>
        /// A unique identifier for purchase events across devices, including subscription-renewal events. This value is the primary key for identifying subscription purchases.
        /// </summary>
        public string WebOrderLineItemId { get; set; }

        /// <summary>
        /// A unique identifier for a transaction such as a purchase, restore, or renewal.
        /// </summary>
        public string TransactionId { get; set; }
    }
}
