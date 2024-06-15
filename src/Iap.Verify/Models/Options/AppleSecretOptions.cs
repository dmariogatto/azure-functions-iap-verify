namespace Iap.Verify.Models
{
    public class AppleSecretOptions
    {
        public const string AppleSecretStoreKey = "AppleSecret";

        public string Master { get; set; }

        private string _appSpecific;
        public string AppSpecific
        {
            get => _appSpecific;
            set
            {
                if (_appSpecific == value)
                    return;

                _appSpecific = value;
                _secrets.Clear();

                if (string.IsNullOrWhiteSpace(_appSpecific))
                    return;

                var pairs =
                    _appSpecific
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Split(':', StringSplitOptions.RemoveEmptyEntries))
                        .Where(i => i.Length == 2);

                foreach (var p in pairs)
                    _secrets.Add(p[0], p[1]);
            }
        }

        private readonly Dictionary<string, string> _secrets = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Secrets => _secrets;
    }
}
