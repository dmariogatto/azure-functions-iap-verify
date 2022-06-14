namespace Iap.Verify.Models
{
    public interface IAppleInApp
    {
        string CancellationDate { get; set; }
        string CancellationDateMs { get; set; }
        string CancellationDatePst { get; set; }
        string CancellationReason { get; set; }
        string ExpiresDate { get; set; }
        string ExpiresDateMs { get; set; }
        string ExpiresDatePst { get; set; }
        string IsInIntroOfferPeriod { get; set; }
        string IsTrialPeriod { get; set; }
        string OfferCodeRefName { get; set; }
        string OriginalPurchaseDate { get; set; }
        string OriginalPurchaseDateMs { get; set; }
        string OriginalPurchaseDatePst { get; set; }
        string OriginalTransactionId { get; set; }
        string ProductId { get; set; }
        string PromotionalOfferId { get; set; }
        string PurchaseDate { get; set; }
        string PurchaseDateMs { get; set; }
        string PurchaseDatePst { get; set; }
        string Quantity { get; set; }
        string TransactionId { get; set; }
        string WebOrderLineItemId { get; set; }
    }
}