using System;

namespace Iap.Verify.Models
{
    public static class AppleInAppExtensions
    {
        public static DateTime GetPurchaseDateUtc(this IAppleInApp inApp)
            => EpochMsToDateTimeUtc(inApp?.PurchaseDateMs, DateTime.UnixEpoch).Value;

        public static DateTime? GetExpiresDateUtc(this IAppleInApp inApp)
            => EpochMsToDateTimeUtc(inApp?.ExpiresDateMs);

        public static DateTime? GetCancellationDateUtc(this IAppleInApp inApp)
            => EpochMsToDateTimeUtc(inApp?.CancellationDateMs);

        public static DateTime? EpochMsToDateTimeUtc(string dateMs, DateTime? defaultValue = null)
            => !string.IsNullOrEmpty(dateMs) && long.TryParse(dateMs, out var ms)
               ? DateTime.UnixEpoch.AddMilliseconds(ms)
               : defaultValue;
    }
}
