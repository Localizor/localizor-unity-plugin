using Localizor.LanguageChangeEvent;
using Localizor.Tools.SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace Localizor
{
    public static class LocalizorManager
    {
        private static readonly LocalizorSettings _settings;
        public static string FallbackLanguage { get; private set; }
        public static string LoadedLocale { get; private set; }
        public static LocalizorSettings.LocalizationMode Mode { get; private set; }

        private static readonly Dictionary<string, Dictionary<string, string>> LocalizationDictionary = new();
        private static readonly Dictionary<string, string> AvailableLanguages = new();
        public static readonly UnityEvent OnLanguageChanged = new();
#pragma warning disable 414
        private static bool _finishedLoadingLocalizations;
#pragma warning restore 414
        public static int GameID => _settings.gameID;

        static LocalizorManager()
        {
            _settings = Resources.Load<LocalizorSettings>("LocalizorSettings");
            if (!_settings)
            {
#if UNITY_EDITOR
                _settings = ScriptableObject.CreateInstance<LocalizorSettings>();
                var path = Path.Combine("Assets", "Localizor", "Resources", "LocalizorSettings.asset");
                var assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(_settings, assetPathAndName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
#else
                Debug.LogError("Localizor Settings Not Found!");
#endif
            }

            LoadedLocale = _settings.defaultLanguage;
            FallbackLanguage = _settings.fallbackLanguage;
            Mode = _settings.defaultLocalizationMode;

            LoadLocalizationTables();
        }

        public static void SetFallbackLanguage(string locale)
        {
            if (AvailableLanguages.ContainsKey(locale))
                FallbackLanguage = locale;
            else
                Debug.LogError("Set a fallback language that is not defined");

            OnLanguageChanged.Invoke();
        }

        public static void SetUsedLanguage(string locale)
        {
            if (AvailableLanguages.ContainsKey(locale))
                LoadedLocale = locale;
            else
                Debug.LogError("Set a used language that is not defined");

            OnLanguageChanged.Invoke();
        }

        public static void SetLocalizationModeThroughCommand(int modeIndex)
        {
            var newLocalizationMode = (LocalizorSettings.LocalizationMode)modeIndex;
            SetLocalizationMode(newLocalizationMode);
            Debug.Log($"Localization mode set to {newLocalizationMode}");
        }

        public static void SetLocalizationMode(LocalizorSettings.LocalizationMode mode)
        {
            Mode = mode;
            OnLanguageChanged.Invoke();
        }

        public static string[] GetAvailableLanguages() => AvailableLanguages.Keys.ToArray();

        public static string GetAvailableLanguagesPrettified(string language) => AvailableLanguages[language];

        public static int GetKeyCount() => LocalizationDictionary[FallbackLanguage].Count;

        private static string LoadFile(string file)
        {
            switch (_settings.storageLocation)
            {
                case LocalizorSettings.LocalizorTableStorageLocation.StreamingAssets:
                    return File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "locale", file));
                case LocalizorSettings.LocalizorTableStorageLocation.Resources:
                    return Resources.Load<TextAsset>(Path.Combine("locale", Path.GetFileNameWithoutExtension(file)))?.text;
                default:
                    return "{}";
            }
        }

        public static void LoadLocalizationTables()
        {
            LocalizationDictionary.Clear();
            AvailableLanguages.Clear();

            var availableLanguages = JSON.Parse(LoadFile("locale.json"));
            foreach (var locale in availableLanguages)
                AvailableLanguages.Add(locale.Key.ToLower(), locale.Value);

            foreach (var locale in AvailableLanguages.Keys)
            {
                try
                {
                    if (_settings.ignoredLanguages.Contains(locale)) continue;

                    var dict = new Dictionary<string, string>();
                    LocalizationDictionary.Add(locale.ToLower(), dict);
                    var table = JSON.Parse(LoadFile($"{locale}.json"));

                    foreach (var key in table.Keys)
                    {
                        var k = key.ToLower().Trim();
                        if (!dict.ContainsKey(k))
                            dict.Add(k, table[k]);
                        else
                            Debug.LogError($"Key {k} already exists in table {locale}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading locale {locale}");
                    Debug.LogException(e);
                }
            }

#if UNITY_EDITOR
            LoadTemp();
#else
            if (Debug.isDebugBuild || Environment.GetCommandLineArgs().Contains("-showTempLocalization"))
                LoadTemp();
#endif
            OnLanguageChanged.Invoke();
            _finishedLoadingLocalizations = true;
        }

        public static void LoadTemp(bool invokeLanguageChangeEvent = false)
        {
            foreach (var tempLocalisation in JSON.Parse(LoadFile("temp.json")))
            {
                var key = tempLocalisation.Key.ToLower().Trim();
                if (LocalizationDictionary[FallbackLanguage].ContainsKey(key))
                    LocalizationDictionary[FallbackLanguage][key] = "$TEMP$" + tempLocalisation.Value;
                else
                    LocalizationDictionary[FallbackLanguage].Add(key, "$TEMP$" + tempLocalisation.Value);
            }

            if (invokeLanguageChangeEvent)
                OnLanguageChanged.Invoke();
        }

        public static string GetLocalization(string label)
        {
            if (Mode == LocalizorSettings.LocalizationMode.KeysOnly)
                return "$$" + label;

            var key = label.ToLower();

            if (TryGetLocalizationValue(key, LoadedLocale, out var value))
                return value;

            if (TryGetLocalizationValue(key, FallbackLanguage, out value))
                return Mode == LocalizorSettings.LocalizationMode.GameMode ? value : "$" + value;

            if (Application.isPlaying)
                Debug.LogWarning($"Localization for {key} not found.");

            return Mode == LocalizorSettings.LocalizationMode.GameMode ? label : "$$" + key;
        }

        private static bool TryGetLocalizationValue(string key, string locale, out string value)
        {
            value = string.Empty;
            if (LocalizationDictionary.TryGetValue(locale, out var languageDictionary))
                return languageDictionary.TryGetValue(key, out value);

            return false;
        }

        public static string GetLocalization(string key, object arguments)
        {
            var value = GetLocalization(key);
            var builder = new StringBuilder(value);

            var members = arguments.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.NonPublic | BindingFlags.GetField |
                                                         BindingFlags.GetProperty);

            foreach (var member in members)
            {
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                    continue;

                string replacementValue = member.MemberType switch
                {
                    MemberTypes.Field => ((FieldInfo)member).GetValue(arguments)?.ToString(),
                    MemberTypes.Property => ((PropertyInfo)member).GetValue(arguments)?.ToString(),
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(replacementValue) && IsLocalizedKey(replacementValue))
                    replacementValue = GetLocalization(replacementValue);

                builder.Replace($"{{{member.Name}}}", replacementValue);
            }

            return builder.ToString();
        }

        public static string FormatLocalization(string format, params object[] args)
        {
            if (args == null) return "";

            return string.Format(format, args.Select(arg =>
            {
                var argStr = arg.ToString();
                if (string.IsNullOrEmpty(argStr)) return "";
                if (IsLocalizedKey(argStr))
                    return GetLocalization(argStr);
                return argStr;
            }).ToArray());
        }

        public static bool IsLocalizedKey(string key) =>
            !string.IsNullOrEmpty(key) &&
            LocalizationDictionary[FallbackLanguage].ContainsKey(key.ToLower());

        public static IEnumerator UpdateLocalizorLocale(int id)
        {
            if (_settings.storageLocation == LocalizorSettings.LocalizorTableStorageLocation.Resources &&
                !Application.isEditor) yield break;

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Download Localizor Update", "", -1);
#endif

            using var webRequest = UnityWebRequest.Get($"https://www.localizor.com/api/v1/public/games/{id}/languages");
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download languages: {webRequest.error}");
                yield break;
            }

            var jsonResponse = JSON.Parse(webRequest.downloadHandler.text);
            var availableLanguages = jsonResponse["data"]["languages"];
            var path = Path.Combine(Application.streamingAssetsPath, "locale");

            if (_settings.storageLocation == LocalizorSettings.LocalizorTableStorageLocation.Resources)
                path = Path.Combine(Application.dataPath, "Resources", "locale");

            Directory.CreateDirectory(path);

            JSONNode locale = new JSONObject();
            var yieldList = new List<IEnumerator>();

            foreach (JSONNode language in availableLanguages)
            {
                string languageCode = language["isoCode"];
                string languageName = language["name"];
                string translationsUrl = language["translations"]["links"]["related"];

                yieldList.Add(DownloadAndParseLanguageData(translationsUrl, languageCode, languageName, path, locale));
            }

            foreach (var operation in yieldList)
            {
                yield return operation;
            }

            File.WriteAllText(Path.Combine(path, "locale.json"), locale.ToString(4));

#if UNITY_EDITOR
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorApplication.Beep();
#endif

            LoadLocalizationTables();
        }

        private static IEnumerator DownloadAndParseLanguageData(string url, string languageCode, string languageName, string path, JSONNode locale)
        {
            var queryParams = $"&includeAllSuggestions={_settings.includeAllSuggestions.ToString().ToLower()}" +
                              $"&includeGoogleTranslate={_settings.includeGoogleTranslate.ToString().ToLower()}" +
                              $"&includeAllKeys={_settings.includeAllKeys.ToString().ToLower()}";

            var fullUrl = url + queryParams;

            using var request = UnityWebRequest.Get(fullUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download language data for {languageName}: {request.error}");
                yield break;
            }

            var jsonResponse = JSON.Parse(request.downloadHandler.text);
            var translations = jsonResponse["data"]["translations"];
            var structuredTranslations = new JSONObject();

            foreach (var translation in translations)
            {
                structuredTranslations.Add(translation.Key, translation.Value);
            }

            var filePath = Path.Combine(path, $"{languageCode.ToLower()}.json");
            File.WriteAllText(filePath, structuredTranslations.ToString(4));

            Debug.Log($"Localization for {languageName} was updated");
            locale.Add(languageCode.ToLower(), languageName);
        }

        public static float GetLanguageLocalizationProgress(string lang)
        {
            if (!_finishedLoadingLocalizations) return 0;

            if (!LocalizationDictionary.TryGetValue(lang, out var loadedLangDictionary))
                return 0;

            float langCount = loadedLangDictionary.Count;
            float fallbackLangCount = LocalizationDictionary[FallbackLanguage].Count;

            return langCount / fallbackLangCount;
        }

        public static LanguageChangeEventDataHolder Localize(this string key, object arguments = null) =>
            LanguageChangeEventDataHolder.Create(key, arguments);
    }
}
