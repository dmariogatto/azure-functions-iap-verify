namespace Iap.Verify.Models
{
    public class ValidationResult
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

        public bool IsValid { get; set; }
        public string Message { get; set; }

        public ValidatedReceipt ValidatedReceipt { get; set; }
    }
}
