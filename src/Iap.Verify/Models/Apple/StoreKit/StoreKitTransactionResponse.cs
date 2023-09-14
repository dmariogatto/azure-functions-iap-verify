namespace Iap.Verify.Models
{
    /// <summary>
    /// See, https://developer.apple.com/documentation/appstoreserverapi/transactioninforesponse
    /// </summary>
    public class StoreKitTransactionResponse
    {
        public string SignedTransactionInfo { get; set; }
    }
}
