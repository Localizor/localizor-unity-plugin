using UnityEditor;
using UnityEngine;

namespace Localizor.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LocalizorSettings))]
    public class LocalizorSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            GUILayout.Label("You can also Open the Editor under Localizor > Open Editor");
            if (GUILayout.Button("Open Editor"))
                LocalizorEditor.Init();
        }
    }
}