using System;
using System.Collections.Generic;
using System.Linq;

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
                Secrets.Clear();

                if (string.IsNullOrWhiteSpace(_appSpecific))
                    return;

                var secretPairs =
                    _appSpecific
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(i => i.Split(':', StringSplitOptions.RemoveEmptyEntries));

                foreach (var pair in secretPairs)
                {
                    if (pair.Length == 2)
                    {
                        Secrets.Add(pair[0], pair[1]);
                    }
                }
            }
        }

        public Dictionary<string, string> Secrets { get; } = new Dictionary<string, string>();
    }
}
