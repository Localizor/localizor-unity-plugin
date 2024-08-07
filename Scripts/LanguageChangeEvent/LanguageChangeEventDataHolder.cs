using System;

namespace Localizor.LanguageChangeEvent
{
    [Serializable]
    public struct LanguageChangeEventDataHolder
    {
        public string Key;
        public string Prefix;
        public string Suffix;
        public object Arguments;
        public string Format;

        public override string ToString()
        {
            var p = Prefix;
            if (LocalizorManager.IsLocalizedKey(p))
                p = LocalizorManager.GetLocalization(p);

            var s = Suffix;
            if (LocalizorManager.IsLocalizedKey(s))
                s = LocalizorManager.GetLocalization(s);
            string text;
            if (!string.IsNullOrEmpty(Format))
                text = LocalizorManager.FormatLocalization(Format, (object[])Arguments);
            else
                text = Arguments == null
                    ? LocalizorManager.GetLocalization(Key)
                    : LocalizorManager.GetLocalization(Key, Arguments);

            return p + text + s;
        }

        public static LanguageChangeEventDataHolder Create(string key, object arguments = null, string prefix = "",
            string suffix = "", string format = "") => new()
        {
            Key = key,
            Prefix = prefix,
            Suffix = suffix,
            Arguments = arguments,
            Format = format
        };
    }
}