#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kamgam.MeshExtractor
{
    // Create a new type of Settings Asset.
    public class MeshExtractorSettings : ScriptableObject
    {
        public const string Version = "1.3.0";
        public const string SettingsFilePath = "Assets/MeshExtractorSettings.asset";

        [SerializeField, Tooltip(_logLevelTooltip)]
        public Logger.LogLevel LogLevel;
        public const string _logLevelTooltip = "Any log above this log level will not be shown. To turn off all logs choose 'NoLogs'";

        [SerializeField, Tooltip(_scrollWheelSensitivityTooltip)]
        public float ScrollWheelSensitivity;
        public const string _scrollWheelSensitivityTooltip = "The sensitivity of the scroll wheel (determines how fast it will change the brush size).";

        [SerializeField, Tooltip(_extractedFilesLocationTooltip)]
        public string ExtractedFilesLocation;
        public const string _extractedFilesLocationTooltip = "The location (relative to Assets/) where the extracted files are stored. This string is appeneded to the file name you choose in the extraction dialog.";
        
        [SerializeField, Tooltip(_logFilePathsTooltip)]
        public bool LogFilePaths;
        public const string _logFilePathsTooltip = "Should the created file paths be logged in the console?";

        [SerializeField, Tooltip(_disableOnHierarchyChangeTooltip)]
        public bool DisableOnHierarchyChange;
        public const string _disableOnHierarchyChangeTooltip = "Disable the tool if the hierarchy changes (convenience). Disable if it annoys you.";

        [SerializeField, Tooltip(_triggerSelectLinkedTooltip)]
        public KeyCode TriggerSelectLinked;
        public const string _triggerSelectLinkedTooltip = "Pressing this key while in 'Select Polygons' mode triggers the 'Select Linked' action.";

        [SerializeField, Tooltip(_maxSelectLinkedDuration)]
        public int MaxSelectLinkedDuration = 10;
        public const string _maxSelectLinkedDuration = "Select linked may take a long time for highpoly meshes. This limits the time it is allowed to take (in seconds).";

        [SerializeField, Tooltip(_showSelectLinkedFailedPopup)]
        public bool ShowSelectLinkedFailedPopup = true;
        public const string _showSelectLinkedFailedPopup = "Should there be a popup window if select linked failed? If turned off then a log message will be used instead.";

        [Range(0,1)]
        public float SelectionColorAlpha;

        [SerializeField, Tooltip(_warnAboutOldSelectionsTooltip)]
        public bool WarnAboutOldSelections;
        public const string _warnAboutOldSelectionsTooltip = "Show a warning dialog if an old selection exists?";

        [SerializeField, Tooltip(_clearOldSelectionsAutomaticallyTooltip)]
        public bool ClearOldSelectionsAutomatically;
        public const string _clearOldSelectionsAutomaticallyTooltip = "Clear old selections automatically? If enabled then the WarnAboutOldSelections will have no effect since they are cleared automatically.";

        public Vector2 WindowPosition;

        [RuntimeInitializeOnLoadMethod]
        static void bindLoggerLevelToSetting()
        {
            // Notice: This does not yet create a setting instance!
            Logger.OnGetLogLevel = () => GetOrCreateSettings().LogLevel;
        }

        [InitializeOnLoadMethod]
        static void autoCreateSettings()
        {
            GetOrCreateSettings();
        }

        static MeshExtractorSettings cachedSettings;

        public static MeshExtractorSettings GetOrCreateSettings()
        {
            if (cachedSettings == null)
            {
                string typeName = typeof(MeshExtractorSettings).Name;

                cachedSettings = AssetDatabase.LoadAssetAtPath<MeshExtractorSettings>(SettingsFilePath);

                // Still not found? Then search for it.
                if (cachedSettings == null)
                {
                    string[] results = AssetDatabase.FindAssets("t:" + typeName);
                    if (results.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(results[0]);
                        cachedSettings = AssetDatabase.LoadAssetAtPath<MeshExtractorSettings>(path);
                    }
                }

                if (cachedSettings != null)
                {
                    SessionState.EraseBool(typeName + "WaitingForReload");
                }

                // Still not found? Then create settings.
                if (cachedSettings == null)
                {
                    CompilationPipeline.compilationStarted -= onCompilationStarted;
                    CompilationPipeline.compilationStarted += onCompilationStarted;

                    // Are the settings waiting for a recompile to finish? If yes then return null;
                    // This is important if an external script tries to access the settings before they
                    // are deserialized after a re-compile.
                    bool isWaitingForReloadAfterCompilation = SessionState.GetBool(typeName + "WaitingForReload", false);
                    if (isWaitingForReloadAfterCompilation)
                    {
                        Debug.LogWarning(typeName + " is waiting for assembly reload.");
                        return null;
                    }

                    cachedSettings = ScriptableObject.CreateInstance<MeshExtractorSettings>();
                    cachedSettings.LogLevel = Logger.LogLevel.Warning;
                    cachedSettings.ScrollWheelSensitivity = 1f;
                    cachedSettings.WindowPosition = new Vector2(-1, -1);
                    cachedSettings.ExtractedFilesLocation = "ExtractedMeshes/";
                    cachedSettings.LogFilePaths = false;
                    cachedSettings.DisableOnHierarchyChange = true;
                    cachedSettings.TriggerSelectLinked = KeyCode.S;
                    cachedSettings.MaxSelectLinkedDuration = 10;
                    cachedSettings.SelectionColorAlpha = 0.5f;
                    cachedSettings.ShowSelectLinkedFailedPopup = true;
                    cachedSettings.WarnAboutOldSelections = true;
                    cachedSettings.ClearOldSelectionsAutomatically = false;

                    AssetDatabase.CreateAsset(cachedSettings, SettingsFilePath);
                    AssetDatabase.SaveAssets();

                    onSettingsCreated();

                    Logger.OnGetLogLevel = () => cachedSettings.LogLevel;
                }
            }

            return cachedSettings;
        }

        private static void onCompilationStarted(object obj)
        {
            string typeName = typeof(MeshExtractorSettings).Name;
            SessionState.SetBool(typeName + "WaitingForReload", true);
        }

        // We use this callback instead of CompilationPipeline.compilationFinished because
        // compilationFinished runs before the assemply has been reloaded but DidReloadScripts
        // runs after. And only after we can access the Settings asset.
        [UnityEditor.Callbacks.DidReloadScripts(999000)]
        public static void DidReloadScripts()
        {
            string typeName = typeof(MeshExtractorSettings).Name;
            SessionState.EraseBool(typeName + "WaitingForReload");
        }

        static void onSettingsCreated()
        {
            bool openManual = EditorUtility.DisplayDialog(
                    "Mesh Extractor",
                    "Thank you for choosing Mesh Extractor.\n\n" +
                    "You'll find the tool under Tools > Mesh Extractor > Start\n\n" +
                    "Please start by reading the manual.\n\n" +
                    "It would be great if you could find the time to leave a review.",
                    "Open manual", "Cancel"
                    );

            if (openManual)
            {
                OpenManual();
            }
        }

        [MenuItem("Tools/Mesh Extractor/Manual", priority = 101)]
        public static void OpenManual()
        {
            Application.OpenURL("https://kamgam.com/unity/MeshExtractorManual.pdf");
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        [MenuItem("Tools/Mesh Extractor/Settings", priority = 100)]
        public static void OpenSettings()
        {
            var settings = MeshExtractorSettings.GetOrCreateSettings();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Mesh Extractor Settings could not be found or created.", "Ok");
            }
        }

        [MenuItem("Tools/Mesh Extractor/Please leave a review :-)", priority = 410)]
        public static void LeaveReview()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/245135?aid=1100lqC54&pubref=asset");
        }

        [MenuItem("Tools/Mesh Extractor/More Asset by KAMGAM", priority = 420)]
        public static void MoreAssets()
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/37829?aid=1100lqC54&pubref=asset");
        }

        [MenuItem("Tools/Mesh Extractor/Version: " + Version, priority = 510)]
        public static void LogVersion()
        {
            Debug.Log("Mesh Extractor Version: " + Version);
        }

        public void Save()
        {
            EditorUtility.SetDirty(this);
#if UNITY_2021_2_OR_NEWER
            AssetDatabase.SaveAssetIfDirty(this);
#else
            AssetDatabase.SaveAssets();
#endif
        }

    }


#if UNITY_EDITOR
    [CustomEditor(typeof(MeshExtractorSettings))]
    public class MeshExtractorSettingsEditor : Editor
    {
        public MeshExtractorSettings settings;

        public void OnEnable()
        {
            settings = target as MeshExtractorSettings;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Version: " + MeshExtractorSettings.Version);
            base.OnInspectorGUI();
        }
    }
#endif

    static class MeshExtractorSettingsProvider
    {
        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateMeshExtractorSettingsProvider()
        {
            var provider = new UnityEditor.SettingsProvider("Project/Mesh Extractor", SettingsScope.Project)
            {
                label = "Mesh Extractor",
                guiHandler = (searchContext) =>
                {
                    var settings = MeshExtractorSettings.GetSerializedSettings();

                    var style = new GUIStyle(GUI.skin.label);
                    style.wordWrap = true;

                    EditorGUILayout.LabelField("Version: " + MeshExtractorSettings.Version);
                    if (drawButton(" Open Manual ", icon: "_Help"))
                    {
                        MeshExtractorSettings.OpenManual();
                    }

                    drawField("LogLevel", "Log Level", MeshExtractorSettings._logLevelTooltip, settings, style);
                    drawField("ScrollWheelSensitivity", "Scroll Wheel Sensitivity", MeshExtractorSettings._scrollWheelSensitivityTooltip, settings, style);
                    drawField("ExtractedFilesLocation", "Extracted Files Location", MeshExtractorSettings._extractedFilesLocationTooltip, settings, style);
                    drawField("LogFilePaths", "Log File Paths", MeshExtractorSettings._logFilePathsTooltip, settings, style);
                    drawField("DisableOnHierarchyChange", "Disable On Hierarchy Change", MeshExtractorSettings._disableOnHierarchyChangeTooltip, settings, style);
                    drawField("TriggerSelectLinked", "Trigger Select Linked", MeshExtractorSettings._triggerSelectLinkedTooltip, settings, style);
                    drawField("MaxSelectLinkedDuration", "Max Select Linked Duration", MeshExtractorSettings._maxSelectLinkedDuration, settings, style);
                    drawField("ShowSelectLinkedFailedPopup", "Show Select Linked Failed Popup", MeshExtractorSettings._showSelectLinkedFailedPopup, settings, style);
                    drawField("SelectionColorAlpha", "Selection Color Alpha", null, settings, style);
                    drawField("WarnAboutOldSelections", "Warn about old selections", MeshExtractorSettings._warnAboutOldSelectionsTooltip, settings, style);
                    drawField("ClearOldSelectionsAutomatically", "Clear Old Selections Automatically", MeshExtractorSettings._clearOldSelectionsAutomaticallyTooltip, settings, style);

                    settings.ApplyModifiedProperties();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting.
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "shader", "triplanar", "rendering" })
            };

            return provider;
        }

        static void drawField(string propertyName, string label, string tooltip, SerializedObject settings, GUIStyle style)
        {
            EditorGUILayout.PropertyField(settings.FindProperty(propertyName), new GUIContent(label));
            if (!string.IsNullOrEmpty(tooltip))
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(tooltip, style);
                GUILayout.EndVertical();
            }
            GUILayout.Space(10);
        }

        static bool drawButton(string text, string tooltip = null, string icon = null, params GUILayoutOption[] options)
        {
            GUIContent content;

            // icon
            if (!string.IsNullOrEmpty(icon))
                content = EditorGUIUtility.IconContent(icon);
            else
                content = new GUIContent();

            // text
            content.text = text;

            // tooltip
            if (!string.IsNullOrEmpty(tooltip))
                content.tooltip = tooltip;

            return GUILayout.Button(content, options);
        }
    }
}
#endif