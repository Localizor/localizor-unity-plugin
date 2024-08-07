using System.Text;
using TMPro;
using UnityEngine;

namespace Localizor.LanguageChangeEvent
{
    [RequireComponent(typeof(TMP_Text))]
    public class TextLocalizationComponent : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private object arguments;
        [SerializeField] private string prefix;
        [SerializeField] private string suffix;
        [SerializeField] private string format;

        private TMP_Text _textContainer;

        public string Key
        {
            get => key;
            set
            {
                if (key == value) return;
                key = value;
                OnUpdateText();
            }
        }

        public object Arguments
        {
            get => arguments;
            set
            {
                if (arguments == value) return;
                arguments = value;
                OnUpdateText();
            }
        }

        public string Prefix
        {
            get => prefix;
            set
            {
                if (prefix == value) return;
                prefix = value;
                OnUpdateText();
            }
        }

        public string Suffix
        {
            get => suffix;
            set
            {
                if (suffix == value) return;
                suffix = value;
                OnUpdateText();
            }
        }

        public string Format
        {
            get => format;
            set
            {
                if (format == value) return;
                format = value;
                OnUpdateText();
            }
        }

        public TMP_Text TextContainer
        {
            get
            {
                if (_textContainer == null) _textContainer = GetComponent<TMP_Text>();
                return _textContainer;
            }
        }

        public void OnEnable()
        {
            OnUpdateText();
            LocalizorManager.OnLanguageChanged.AddListener(OnUpdateText);
        }

        public void SetValue(string text)
        {
            TextContainer.text = text;
        }

        public void SetData(LanguageChangeEventDataHolder data)
        {
            key = data.Key;
            arguments = data.Arguments;
            prefix = data.Prefix;
            suffix = data.Suffix;
            format = data.Format;
            OnUpdateText();
        }

        public void OnValidate()
        {
            if (string.IsNullOrEmpty(key)) return;

            key = key.ToLower().Trim(' ');
            if (TextContainer)
                SetValue(Prefix + key + Suffix);
        }

        public void OnDisable() => LocalizorManager.OnLanguageChanged.RemoveListener(OnUpdateText);

        private void OnUpdateText()
        {
            if (!Application.isPlaying)
            {
                SetValue(Key);
                return;
            }

            if (!string.IsNullOrEmpty(Format))
            {
                FormatLocalisation();
                return;
            }

            if (string.IsNullOrEmpty(Key) || !TextContainer)
                return;

            var localizedText = Arguments == null
                ? LocalizorManager.GetLocalization(Key)
                : LocalizorManager.GetLocalization(Key, Arguments);

            var finalText = new StringBuilder();
            finalText.Append(GetLocalizedPrefix(Prefix));
            finalText.Append(localizedText);
            finalText.Append(GetLocalizedSuffix(Suffix));

            SetValue(finalText.ToString());
        }

        private string GetLocalizedPrefix(string prefix)
        {
            if (!string.IsNullOrEmpty(prefix) && LocalizorManager.IsLocalizedKey(prefix))
                return LocalizorManager.GetLocalization(prefix);
            return prefix;
        }

        private string GetLocalizedSuffix(string suffix)
        {
            if (!string.IsNullOrEmpty(suffix) && LocalizorManager.IsLocalizedKey(suffix))
                return LocalizorManager.GetLocalization(suffix);
            return suffix;
        }

        private void FormatLocalisation()
        {
            if (!TextContainer)
                return;

            var p = Prefix;
            if (!string.IsNullOrEmpty(p) && LocalizorManager.IsLocalizedKey(p))
                p = LocalizorManager.GetLocalization(p);

            var s = Suffix;
            if (!string.IsNullOrEmpty(s) && LocalizorManager.IsLocalizedKey(s))
                s = LocalizorManager.GetLocalization(s);

            var text = LocalizorManager.FormatLocalization(format, (object[])Arguments);
            SetValue(p + text + s);
        }
    }
}