using System;

namespace Iap.Verify.Models
{
    public static class AppleInAppExtensions
    {
        public static DateTime GetPurchaseDateUtc(this IAppleInApp inApp)
            => long.TryParse(inApp?.PurchaseDateMs, out var ms)
               ? DateTime.UnixEpoch.AddMilliseconds(ms)
               : DateTime.UnixEpoch;
        public static DateTime? GetExpiresDateUtc(this IAppleInApp inApp)
            => long.TryParse(inApp?.ExpiresDateMs, out var ms)
               ? DateTime.UnixEpoch.AddMilliseconds(ms)
               : (DateTime?)null;
        public static DateTime? GetCancellationDateUtc(this IAppleInApp inApp)
            => long.TryParse(inApp?.CancellationDateMs, out var ms)
               ? DateTime.UnixEpoch.AddMilliseconds(ms)
               : (DateTime?)null;
    }
}
