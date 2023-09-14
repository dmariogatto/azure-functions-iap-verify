namespace Iap.Verify.Models
{
    /// <summary>
    /// See, https://developer.apple.com/documentation/appstoreserverapi/jwstransactiondecodedpayload
    /// </summary>
    public class StoreKitTransactionInfo
    {
        /// <summary>
        /// The unique identifier of the transaction.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// The transaction identifier of the original purchase.
        /// </summary>
        public string OriginalTransactionId { get; set; }

        /// <summary>
        /// The unique identifier of subscription purchase events across devices, including subscription renewals.
        /// </summary>
        public string WebOrderLineItemId { get; set; }

        /// <summary>
        /// The bundle identifier of the app.
        /// </summary>
        public string BundleId { get; set; }

        /// <summary>
        /// The unique identifier of the product.
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        /// The identifier of the subscription group the subscription belongs to.
        /// </summary>
        public string SubscriptionGroupIdentifier { get; set; }

        /// <summary>
        /// The UNIX time, in milliseconds, that the App Store charged the user’s account for a purchase, restored product, subscription, or subscription renewal after a lapse.
        /// </summary>
        public long PurchaseDate { get; set; }

        /// <summary>
        /// The UNIX time, in milliseconds, that represents the purchase date of the original transaction identifier.
        /// </summary>
        public long OriginalPurchaseDate { get; set; }

        /// <summary>
        /// The UNIX time, in milliseconds, the subscription expires or renews.
        /// </summary>
        public long? ExpiresDate { get; set; }

        /// <summary>
        /// The UNIX time, in milliseconds, that the App Store refunded the transaction or revoked it from Family Sharing.
        /// </summary>
        public long? RevocationDate { get; set; }

        /// <summary>
        /// The reason that the App Store refunded the transaction or revoked it from Family Sharing.
        /// </summary>
        public string RevocationReason { get; set; }

        /// <summary>
        /// The number of consumable products the user purchased.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// The type of the in-app purchase.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// A string that describes whether the transaction was purchased by the user, or is available to them through Family Sharing.
        /// </summary>
        public string InAppOwnershipType { get; set; }

        /// <summary>
        /// The UNIX time, in milliseconds, that the App Store signed the JSON Web Signature (JWS) data.
        /// </summary>
        public long SignedDate { get; set; }

        /// <summary>
        /// The server environment, either sandbox or production.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// The reason for the purchase transaction, which indicates whether it’s a customer’s purchase or a renewal for an auto-renewable subscription that the system initates.
        /// </summary>
        public string TransactionReason { get; set; }

        /// <summary>
        /// The three-letter code that represents the country or region associated with the App Store storefront for the purchase.
        /// </summary>
        public string Storefront { get; set; }

        /// <summary>
        /// An Apple-defined value that uniquely identifies the App Store storefront associated with the purchase.
        /// </summary>
        public string StorefrontId { get; set; }
    }
}
