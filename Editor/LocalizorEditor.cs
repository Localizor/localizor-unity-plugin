using Localizor.LanguageChangeEvent;
using System;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace Localizor.Editor
{
    public class LocalizorEditor : EditorWindow
    {
        private static LocalizorEditor Window;
        public LocalizorSettings settings;
        public bool foldout;
        public bool statisticsFoldout;
        public bool updateParameterFoldout;
        public bool findKeyUsagesFoldout;
        public Vector2 scrollPosition;

        private string localizationKeyToFind;

        [MenuItem("Window/Localizor/Open Settings")]
        public static void Init()
        {
            if (Window)
                Window.Close();
            Window = (LocalizorEditor)GetWindow(typeof(LocalizorEditor), true, "Localizor Settings", true);
            Window.Show();
        }

        private void Awake()
        {
            minSize = new Vector2(320, 220);
            settings = Resources.Load<LocalizorSettings>("LocalizorSettings");
            if (settings == null)
            {
                settings = CreateInstance<LocalizorSettings>();
                AssetDatabase.CreateAsset(settings, "Assets/Localizor/Resources/LocalizorSettings.asset");
                AssetDatabase.SaveAssets();
            }
        }

        private void OnGUI()
        {
            settings.storageLocation =
                (LocalizorSettings.LocalizorTableStorageLocation)EditorGUILayout.EnumPopup("Storage Location",
                    settings.storageLocation);
            settings.gameID = EditorGUILayout.IntField("Game ID", settings.gameID);

            var availableLanguages = LocalizorManager.GetAvailableLanguages();

            var defaultLanguageIndex = Array.IndexOf(availableLanguages, settings.defaultLanguage);
            var fallbackLanguageIndex = Array.IndexOf(availableLanguages, settings.fallbackLanguage);
            defaultLanguageIndex = EditorGUILayout.Popup("Default Language", defaultLanguageIndex, availableLanguages);
            fallbackLanguageIndex =
                EditorGUILayout.Popup("Fallback Language", fallbackLanguageIndex, availableLanguages);
            if (defaultLanguageIndex != -1)
                settings.defaultLanguage = availableLanguages[defaultLanguageIndex];
            if (fallbackLanguageIndex != -1)
                settings.fallbackLanguage = availableLanguages[fallbackLanguageIndex];

            updateParameterFoldout = EditorGUILayout.Foldout(updateParameterFoldout, "Update Parameter");
            if (updateParameterFoldout)
            {
                settings.includeAllKeys = EditorGUILayout.Toggle("Include All Keys", settings.includeAllKeys);
                settings.includeAllSuggestions =
                    EditorGUILayout.Toggle("Include All Suggestions", settings.includeAllSuggestions);
                settings.includeGoogleTranslate =
                    EditorGUILayout.Toggle("Include Google Translate", settings.includeGoogleTranslate);
            }

            settings.defaultLocalizationMode =
                (LocalizorSettings.LocalizationMode)EditorGUILayout.EnumPopup("Default Localization Mode",
                    settings.defaultLocalizationMode);
            foldout = EditorGUILayout.Foldout(foldout, "Ignored Languages");
            if (foldout)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                var ignored = availableLanguages.Select(x
                    => EditorGUILayout.Toggle(
                        $"{x}({Mathf.Round(LocalizorManager.GetLanguageLocalizationProgress(x) * 1000) / 10}%)",
                        settings.ignoredLanguages.Contains(x))).ToList();
                settings.ignoredLanguages.Clear();
                for (var i = 0; i < ignored.Count; i++)
                    if (ignored[i])
                        settings.ignoredLanguages.Add(availableLanguages[i]);
                EditorGUILayout.EndScrollView();
            }

            statisticsFoldout = EditorGUILayout.Foldout(statisticsFoldout, "Statistics");
            if (statisticsFoldout)
                if (settings && fallbackLanguageIndex != -1)
                {
                    GUILayout.Label($"Languages: {LocalizorManager.GetAvailableLanguages().Length}",
                        EditorStyles.largeLabel);
                    GUILayout.Label($"Keys: {LocalizorManager.GetKeyCount()}", EditorStyles.largeLabel);
                }

            findKeyUsagesFoldout = EditorGUILayout.Foldout(findKeyUsagesFoldout, "Find key usages in open scenes");
            if (findKeyUsagesFoldout)
                if (settings && fallbackLanguageIndex != -1)
                {
                    localizationKeyToFind = EditorGUILayout.TextField("Localization key:", localizationKeyToFind);
                    if (GUILayout.Button("Search key"))
                        if (!string.IsNullOrEmpty(localizationKeyToFind))
                        {
                            var keyFound = false;
                            foreach (var tmpText in FindObjectsOfType<TextLocalizationComponent>(true))
                                if (tmpText.Key == localizationKeyToFind)
                                {
                                    var tmpTransform = tmpText.transform;
                                    Debug.LogError($"Key usage found ({tmpTransform.name}/{tmpTransform.parent.name})",
                                        tmpText);
                                    keyFound = true;
                                }

                            if (!keyFound) Debug.LogError($"No key usages found for '{localizationKeyToFind}'");
                        }
                }

            GUILayout.Space(10);

            if (GUILayout.Button("Save Settings"))
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (GUILayout.Button("Download Localizor Stringtables"))
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorCoroutineUtility.StartCoroutine(LocalizorManager.UpdateLocalizorLocale(LocalizorManager.GameID),
                    this);
            }

            if (GUILayout.Button("Reload Localizor Stringtables"))
                LocalizorManager.LoadLocalizationTables();
        }

        [MenuItem("Window/Localizor/Update Stringtables")]
        private static void UpdateLocalizorLocale()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(
                LocalizorManager.UpdateLocalizorLocale(LocalizorManager.GameID));
        }
    }
}
