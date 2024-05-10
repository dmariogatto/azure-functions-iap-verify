namespace Iap.Verify.Models
{
    public record ValidationResult
    {
        public ValidationResult(bool isValid, string msg)
        {
            IsValid = isValid;
            Message = msg ?? string.Empty;
        }

        public ValidationResult(bool isValid)
        {
            IsValid = isValid;
            Message = string.Empty;
        }

        public bool IsValid { get; init; }
        public string Message { get; init; }

        public ValidatedReceipt ValidatedReceipt { get; init; }
    }
}
