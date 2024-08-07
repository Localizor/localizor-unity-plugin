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
                var path = Path.Combine("Assets", "Resources", "LocalizorSettings.asset");
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
                Debug.LogError("Set a fallback language that does not defined");
            OnLanguageChanged.Invoke();
        }

        public static void SetUsedLanguage(string locale)
        {
            if (AvailableLanguages.ContainsKey(locale))
                LoadedLocale = locale;
            else
                Debug.LogError("Set a used language that does not defined");
            OnLanguageChanged.Invoke();
        }

        public static void SetLocalizationModeThroughCommand(int modeIndex)
        {
            var newLocalizationMode = (LocalizorSettings.LocalizationMode)modeIndex;
            SetLocalizationMode(newLocalizationMode);
            Debug.Log($"Localization mode set to {newLocalizationMode.ToString()}");
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
                    return File.ReadAllText(
                        Path.Combine(Path.Combine(Application.streamingAssetsPath, "locale"), file));
                case LocalizorSettings.LocalizorTableStorageLocation.Resources:
                    return Resources
                        .Load<TextAsset>(Path.Combine("locale", Path.GetFileNameWithoutExtension(file)))
                        .text;
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
                try
                {
                    if (_settings.ignoredLanguages.Contains(locale)) continue;
                    var dict = new Dictionary<string, string>();
                    LocalizationDictionary.Add(locale.ToLower(), dict);
                    var table = JSON.Parse(LoadFile(locale + ".json"));
                    foreach (var key in table.Keys)
                    {
                        var k = key.ToLower().Trim();
                        if (!dict.ContainsKey(k))
                            dict.Add(k, table[k]);
                        else
                            Debug.LogError($"Key {k} already in table {locale}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading locale {locale}");
                    Debug.LogException(e);
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
                    // (David) Commenting as it was spamming the log Debug.Log($"Try to add Duplicated key: {key};{tempLocalisation.Value}");
                    LocalizationDictionary[FallbackLanguage][key] = "$TEMP$" + tempLocalisation.Value;
                else
                    LocalizationDictionary[FallbackLanguage].Add(key, "$TEMP$" + tempLocalisation.Value);
            }

            if (invokeLanguageChangeEvent)
                OnLanguageChanged.Invoke();
        }

        public static string GetLocalization(string label)
        {
            if (Mode == LocalizorSettings.LocalizationMode.KeysOnly) return "$$" + label;
            var key = label.ToLower();

            if (TryGetLocalizationValue(key, LoadedLocale, out var value))
                return value;

            if (TryGetLocalizationValue(key, FallbackLanguage, out value))
                return Mode == LocalizorSettings.LocalizationMode.GameMode ? value : "$" + value;

            if (Application.isPlaying) Debug.LogWarning($"Localization for {key} not Found.");
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

                string replacementValue;

                if (member.MemberType == MemberTypes.Field)
                    replacementValue = ((FieldInfo)member).GetValue(arguments)?.ToString();
                else // Property
                    replacementValue = ((PropertyInfo)member).GetValue(arguments)?.ToString();

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
                return (object)argStr;
            }).ToArray());
        }

        public static bool IsLocalizedKey(string key) => !string.IsNullOrEmpty(key) &&
                                                         LocalizationDictionary[FallbackLanguage]
                                                             .ContainsKey(key.ToLower());

        public static IEnumerator UpdateLocalizorLocale(int id)
        {
            // NOTE(joko): if we Store the Localizations Tables in the Resources we dont allow downloading them while the editor is Running
            if (_settings.storageLocation == LocalizorSettings.LocalizorTableStorageLocation.Resources &&
                !Application.isEditor) yield break;

#if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Download Localizor Update", "", -1);
#endif
            using var webRequest = UnityWebRequest.Get($"https://www.localizor.com/api/public/{id}/languages");
            var asyncOperation = webRequest.SendWebRequest();
            yield return asyncOperation;
            var availableLanguages = JSON.Parse(webRequest.downloadHandler.text);
            var path = Path.Combine(Application.streamingAssetsPath, "locale");
            if (_settings.storageLocation == LocalizorSettings.LocalizorTableStorageLocation.Resources)
                path = Path.Combine(Application.dataPath, "Resources", "locale");

            Directory.CreateDirectory(path);

            JSONNode locale = new JSONObject();
            var langKeys = availableLanguages.Keys.ToList();
            var yieldList = new List<Tuple<UnityWebRequestAsyncOperation, string, UnityWebRequest>>();
            foreach (var language in langKeys)
            {
                var request = UnityWebRequest.Get(availableLanguages[language] +
                                                  $"&includeAllSuggestions={_settings.includeAllSuggestions.ToString().ToLower()}&includeGoogleTranslate={_settings.includeGoogleTranslate.ToString().ToLower()}&includeAllKeys={_settings.includeAllKeys.ToString().ToLower()}");
                yieldList.Add(
                    new Tuple<UnityWebRequestAsyncOperation, string, UnityWebRequest>(request.SendWebRequest(),
                        language, request));
            }

            foreach (var (yield, lang, request) in yieldList)
            {
                yield return yield;
                var parsedTable = JSON.Parse(yield.webRequest.downloadHandler.text)[0];
                request.Dispose();
                parsedTable["translations"].Inline = false;
                File.WriteAllText(Path.Combine(path, lang.ToLower() + ".json"),
                    parsedTable["translations"].ToString(4));
                Debug.Log($"Localization for {lang} was Updated");
                locale.Add(lang.ToLower(), parsedTable["languageName"]);
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

        public static float GetLanguageLocalizationProgress(string lang)
        {
            if (!_finishedLoadingLocalizations) return 0;
            if (!LocalizationDictionary.TryGetValue(lang, out var loadedLangDictionary))
                return 0;
            float langCount = loadedLangDictionary.Count;
            float fallbackLangCount = LocalizationDictionary[FallbackLanguage].Count;
            return langCount / fallbackLangCount;
        }

        public static LanguageChangeEventDataHolder Localize(this string key, object arguments = null)
            => LanguageChangeEventDataHolder.Create(key, arguments);
    }
}