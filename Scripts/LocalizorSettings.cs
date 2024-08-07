using System.Collections.Generic;
using UnityEngine;

namespace Localizor
{
    [CreateAssetMenu(menuName = "Localizor/Localizor Settings", fileName = "Localizor Settings")]
    public class LocalizorSettings : ScriptableObject
    {
        public enum LocalizorTableStorageLocation
        {
            StreamingAssets,
            Resources
        }

        public enum LocalizationMode
        {
            GameMode,
            HalfTranslated,
            KeysOnly
        }

        public int gameID = -1;
        public LocalizorTableStorageLocation storageLocation = LocalizorTableStorageLocation.StreamingAssets;
        public List<string> ignoredLanguages = new();
        public string defaultLanguage = "en";
        public string fallbackLanguage = "en";
        public LocalizationMode defaultLocalizationMode = LocalizationMode.GameMode;

        public bool includeAllSuggestions = true;
        public bool includeGoogleTranslate = true;
        public bool includeAllKeys;
    }
}