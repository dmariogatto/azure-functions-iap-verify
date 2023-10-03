# In-App Purchase Verification using Azure Functions

How to use: [Verifying In-App Purchases using Azure Functions](https://dgatto.com/posts/2020/05/verifying-iap-azure/)

## Endpoints

### ~/api/v1/Apple
Uses the [verifyreceipt](https://developer.apple.com/documentation/appstorereceipts/verifyreceipt?language=objc) API to validate IAPs with Apple **(Deprecated)**.

### ~/api/v2/Apple
Uses the [App Store Server](https://developer.apple.com/documentation/appstoreserverapi?language=objc) API to validate IAPs with Apple.

### ~/api/v1/Google
Uses the [Google Android Publisher](https://www.nuget.org/packages/Google.Apis.AndroidPublisher.v3) API to validate IAPs with Google.

## App Settings

| Setting                     | Type/Format              | Description                                                                                                                           |
|-----------------------------|--------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| GraceDays                   | INT                      | Number of days to allow an expired/cancelled IAP to remain valid                                                                      |
| Google:Account              | STRING                   | Google Service Account User                                                                                                           |
| Google:KeyBase64            | B64_STRING               | Base64 encoded P8 key for service account                                                                                             |
| AppleSecret:Master          | STRING                   | Master secret for `verifyReceipt` endpoint, fall back when no app specific secret found                                               |
| AppleSecret:AppSpecific     | {BUNDLE_ID}:{SECRET},... | App specific secrets for `verifyReceipt` endpoint  Comma separated dictionary, key (BundleId) and value (secret) separated by a Colon |
| AppleStore:IssuerId         | GUID                     | JWT Issuer ID for StoreKit Server API                                                                                                 |
| AppleStore:KeyId            | STRING                   | JWT Key ID for StoreKit Server API                                                                                                    |
| AppleStore:PrivateKeyBase64 | B64_STRING               | Base64 encoded P8 key for signing JWT for StoreKit API                                                                                |

## Resources

- [InAppBillingPlugin](https://jamesmontemagno.github.io/InAppBillingPlugin/)
- [Securing In-App Purchases for Xamarin with Azure Functions](http://jonathanpeppers.com/Blog/securing-in-app-purchases-for-xamarin-with-azure-functions)
- [Securing Google Play In-App Purchases for Xamarin with Azure Functions](http://jonathanpeppers.com/Blog/securing-google-play-in-app-purchases-for-xamarin-with-azure-functions)