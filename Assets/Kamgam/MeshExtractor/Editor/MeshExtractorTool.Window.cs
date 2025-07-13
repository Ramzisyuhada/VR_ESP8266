using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;

namespace Kamgam.MeshExtractor
{
    public static class BackgroundTexture
    {
        private static Dictionary<Color, Texture2D> textures = new Dictionary<Color, Texture2D>();

        public static Texture2D Get(Color color)
        {
            if (textures.ContainsKey(color) && textures[color] != null) 
                return textures[color];

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            if (textures.ContainsKey(color))
                textures[color] = texture;
            else
                textures.Add(color, texture);

            return texture;
        }
    }

    partial class MeshExtractorTool
    {
        Rect windowRect;

        [System.NonSerialized]
        string _newMeshName = "Mesh";

        [System.NonSerialized]
        bool _replaceOldMesh = true;

        [System.NonSerialized]
        bool _preserveSubMeshes = true;

        [System.NonSerialized]
        bool _combineSubMeshesBasedOnMaterials = true;

        [System.NonSerialized]
        bool _combineMeshes = true;

        [System.NonSerialized]
        bool _saveAsObj = false;
        
        [System.NonSerialized]
        bool _extractTextures = true;
        
        [System.NonSerialized]
        bool _extractBoneWeights = false;
        
        [System.NonSerialized]
        bool _extractBoneTransforms = true;

        [System.NonSerialized]
        bool _extractBlendShapes = false;

        void initWindowSize()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                var settings = MeshExtractorSettings.GetOrCreateSettings();
                windowRect.position = settings.WindowPosition;
                windowRect.width = 250;
                windowRect.height = 90;

                // If the window is not yet set or if it's outside the scene view then reset position.
                if (
                       windowRect.position.x > SceneView.lastActiveSceneView.position.width
                    || windowRect.position.x < 0
                    || windowRect.position.y > SceneView.lastActiveSceneView.position.height
                    || windowRect.position.y < 0
                    )
                {
                    // center
                    windowRect.position = new Vector2(
                        SceneView.lastActiveSceneView.position.width * 0.5f,
                        SceneView.lastActiveSceneView.position.height * 0.5f
                        );
                    settings.WindowPosition = windowRect.position;
                    EditorUtility.SetDirty(settings);
                }
            }
        }

        [MenuItem("Tools/Mesh Extractor/Debug/Recenter Window", priority = 220)]
        static void RecenterWindowMenu()
        {
            if (Instance != null)
                Instance.RecenterWindow();
        }

        public void RecenterWindow()
        {
            var settings = MeshExtractorSettings.GetOrCreateSettings();
            
            // center
            windowRect.position = new Vector2(
                SceneView.lastActiveSceneView.position.width * 0.5f,
                SceneView.lastActiveSceneView.position.height * 0.5f
                );

            // dimensions
            windowRect.width = 250;
            windowRect.height = 90;

            settings.WindowPosition = windowRect.position;
            EditorUtility.SetDirty(settings);

            Logger.LogWarning("Please consider upgrading Unity. There is a bug in Unity 2021.0 to 2021.2.3f1 and 2022.0 - 2022.1.0a15, see: https://issuetracker.unity3d.com/issues/tool-handles-are-invisible-in-scene-view-when-certain-objects-are-selected");
        }

        void drawWindow(SceneView sceneView, int controlID)
        {
            Handles.BeginGUI();

            var oldRect = windowRect;
            windowRect = GUILayout.Window(controlID, windowRect, drawWindowContent, "Mesh Extractor");

            // Auto save window position in settings if changed.
            if (Vector2.SqrMagnitude(oldRect.position - windowRect.position) > 0.01f)
            {
                var settings = MeshExtractorSettings.GetOrCreateSettings();
                settings.WindowPosition = windowRect.position;
                EditorUtility.SetDirty(settings);
            }

            Handles.EndGUI();
        }

        void drawWindowContent(int controlID)
        {
            var settings = MeshExtractorSettings.GetOrCreateSettings();

            var bgColor = UtilsEditor.IsLightTheme() ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.25f, 0.25f, 0.25f);
            var tex = BackgroundTexture.Get(bgColor);
            GUI.DrawTexture(new Rect(5, 22, windowRect.width - 10, windowRect.height - 26), tex);

            BeginHorizontalIndent(5);

            GUILayout.Space(5);

            // close button
            var closeBtnStyle = GUIStyle.none;
            closeBtnStyle.normal.background = BackgroundTexture.Get(UtilsEditor.IsLightTheme() ? new Color(0.45f, 0.45f, 0.45f) : bgColor);
            closeBtnStyle.hover.background = BackgroundTexture.Get(new Color(0.5f, 0.5f, 0.5f));
#if UNITY_2023_1_OR_NEWER
            var closeBtnContent = EditorGUIUtility.IconContent("d_clear@2x");
#else
            var closeBtnContent = EditorGUIUtility.IconContent("d_winbtn_win_close_a@2x");
#endif
            closeBtnContent.tooltip = "Close the tool (Esc).";
            if (GUI.Button(new Rect(windowRect.width - 21, 2, 16, 20), closeBtnContent, closeBtnStyle))
            {
                exitTool(); 
            }

            // tool type buttons
            var deselectedStyle = new GUIStyle(GUI.skin.button);
            deselectedStyle.normal.background = BackgroundTexture.Get(new Color(0.5f, 0.5f, 0.5f));

            // Button bar
            drawButtonsInWindow(deselectedStyle);

            // Content
            switch (_mode)
            {
                case Mode.PickObjects:
                    drawPickObjectsWindowContent();
                    break;

                case Mode.PaintSelection:
                    drawSelectWindowContentGUI();
                    break;

                case Mode.ExtractMesh:
                    drawExtractMeshWindowContentGUI();
                    break;

                default:
                    break;
            }


            GUILayout.Space(2);

            EndHorizontalIndent(bothSides: true);

            GUILayout.Space(4);

            GUI.DragWindow();
        }

        private void drawButtonsInWindow(GUIStyle deselectedStyle)
        {
            GUILayout.BeginHorizontal();

            if (DrawButton("",
                icon: "d_FilterByType@2x",
                style: _mode == Mode.PickObjects ? null : deselectedStyle,
                tooltip: "Select objects",
                options: GUILayout.Height(22)))
            {
                SetMode(Mode.PickObjects);
            }

            GUI.enabled = _selectedObjects.Length > 0;
            if (DrawButton("",
                icon: "d_pick@2x",
                style: _mode == Mode.PaintSelection ? null : deselectedStyle,
                tooltip: "Start painting the selection.",
                options: GUILayout.Height(22)))
            {
                SetMode(Mode.PaintSelection);
            }
            GUI.enabled = true;

            GUI.enabled = _selectedObjects.Length > 0 && _selectedTriangles.Count > 0;
            if (DrawButton("",
                icon: "d_PreMatCube@2x",
                style: _mode == Mode.ExtractMesh ? null : deselectedStyle,
                tooltip: "Extract the selected mesh. Available only if at least one triangle is selected.",
                options: GUILayout.Height(22)))
            {
                SetMode(Mode.ExtractMesh);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        void drawPickObjectsWindowContent()
        {
            DrawLabel("Select objects", bold: true);
            DrawLabel("Select one or more objects to extract meshes from.", "This is useful to avoid selecting background meshes by accident. You can return to this step at any time and add or remove objects.", wordwrap: true);
            
            if (DrawButton("Reset", "Clears the current selection, deselects any object and resets all configurations to default."))
            {
                clearSelection();
                Selection.objects = new GameObject[] { };
                resetSelect();
                resetExtract();
            }
        }

        void drawSelectWindowContentGUI()
        {
            DrawLabel("Select polygons", "Paint on the objects to select polyons.", bold: true);

            GUI.enabled = _selectedObjects.Length > 0;

            _selectCullBack = !GUILayout.Toggle(!_selectCullBack, new GUIContent("X-Ray", "X-Ray mode allows you to select front and back facing triangles at the same time."));

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Brush Size:", "Reduce the brush size to 0 to select only one triangle at a time.\n\nYou can also use SHIFT + MOUSE WHEEL to change the brush size."), GUILayout.MaxWidth(75));
            _selectBrushSize = GUILayout.HorizontalSlider(_selectBrushSize, 0f, 1f);
            GUILayout.Label((_selectBrushSize * 10).ToString("f1", CultureInfo.InvariantCulture), GUILayout.MaxWidth(22));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Brush Depth:", "Brush depth defines how far into the object the selection will go. This helps to avoid selecting background polygons by accident. If you want infinite depth then simply turn on X-Ray."), GUILayout.MaxWidth(75));
            _selectBrushDepth = GUILayout.HorizontalSlider(_selectBrushDepth, 0f, 2f);
            _selectBrushDepth = EditorGUILayout.FloatField(_selectBrushDepth, GUILayout.MaxWidth(32));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _lastSelectedTriangle != null;
            if (DrawButton("Select Linked", "Selects all triangles which are connected to the last selected triangle.\n\nHold SHIFT while clicking the button to deselect linked.\n\nHINT: You can press S or SHIFT + S while selecting to trigger this action."))
            {
                addLinkedToSelection(remove: Event.current.shift);
            }
            if (DrawButton("Deselect", "Deselects all triangles which are connected to the last selected triangle."))
            {
                addLinkedToSelection(remove: true);
            }
            _limitLinkedSearchToSubMesh = EditorGUILayout.ToggleLeft(
                new GUIContent("Limit", "Enable to limit selection to a single sub mesh.\nIt will use the sub mesh of the last selected triangle."),
                _limitLinkedSearchToSubMesh,
                GUILayout.Width(50)
                );
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (DrawButton("Clear", "Clears the current selection."))
            {
                clearSelection();
            }
            if (DrawButton("Invert", "Inverts the current selection."))
            {
                invertSelection();
            }
            GUILayout.EndHorizontal();
        }

        void drawExtractMeshWindowContentGUI()
        {
            GUI.enabled = _selectedObjects.Length > 0 && _selectedTriangles.Count > 0;

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            DrawLabel("Name:", "Enter the file name of the new mesh.", wordwrap: false);
            _newMeshName = EditorGUILayout.TextField(_newMeshName);
            _replaceOldMesh = EditorGUILayout.ToggleLeft(new GUIContent("Replace", "Replace existing meshes? If off then a number will be appended to every new file."), _replaceOldMesh, GUILayout.MaxWidth(65));
            GUILayout.EndHorizontal();

            _preserveSubMeshes = EditorGUILayout.ToggleLeft(
                new GUIContent("Preserve SubMeshes", "Enable to preserve (copy) sub meshes in the new mesh. If disabled then all meshes within one renderer will be merged into a single mesh and the very first material found will be used."),
                _preserveSubMeshes
                );

            GUI.enabled = _preserveSubMeshes;
            _combineSubMeshesBasedOnMaterials = EditorGUILayout.ToggleLeft(
                new GUIContent("Combine SubMeshes by Material", "If multiple sub meshes have the same material assigned to them then these will be merged into one submesh if this option is enabled. This has no effect if 'Preserve SubMeshes' is disabled."),
                _combineSubMeshesBasedOnMaterials
                );
            GUI.enabled = true;

            _combineMeshes = EditorGUILayout.ToggleLeft(
                new GUIContent("Combine Meshes", "If multiple objects (renderers) are selected then this defines whether or not all these meshes should be combined into one mesh. If disabled then the result will be one mesh per selected object."),
                _combineMeshes
                );

            _saveAsObj = EditorGUILayout.ToggleLeft(
                new GUIContent("Save as .obj", "Export the mesh as .obj & .mtl files instead of a .asset file.\nNOTICE: The obj format does only support one set of UVs."),
                _saveAsObj
                );

            _extractTextures = EditorGUILayout.ToggleLeft(
                new GUIContent("Extract Textures", "Extract the parts of the texture which are used by the selection and create a new (possibly smaller) texture from it." +
                "\n\nNOTICE:" +
                "\n* Textures are searched by common property names like '_MainTex' or '_BaseMap'. Please check the manual for more details." +
                "\n* It does ignore tiling and offests set in shaders." + 
                "\n* The reduction of the texture size depends on the original UV layout (it uses a bounding box)."),
                _extractTextures
                );

            GUILayout.BeginHorizontal();
            _extractBoneWeights = EditorGUILayout.ToggleLeft(
               new GUIContent("Extract Bone Weights", "Extracts the bone weights of the source model." +
               "\n\nNOTICE: This also means it will NOT bake the current pose of the mesh. Instead it will export the default pose." +
               "\n\nThe expectation is that you will use this on the same bone setup (aka 'Rig' or 'Armature') you exported it from. " +
               "It will most likely NOT work on another rig. Please read up on skinning and rigging meshes if you are not sure what this means." +
               "\n\nBone weight information is NOT saved in .obj files (it's just not supported by that file format)."
               ),
               _extractBoneWeights,
               GUILayout.Width(160)
               );

            GUI.enabled = _extractBoneWeights;
            _extractBoneTransforms = EditorGUILayout.ToggleLeft(
               new GUIContent("Transf.", "Adds a copy of the bone transforms to the exported prefab." +
               "\n\nUse this if you want to export the bone transforms along with the object. This means you will have a fully functional rig (if it was functional before)." +
               "\n\nNOTICE: If you turn this off then you will have to create (copy from original) or assign new bones that match the bind poses yourself." +
               "\n\nIf you are not sure what this means then please read up on rigging / skinning meshes in Unity (it's just too much to explain in a tiny tooltip, sorry)."
               ),
               _extractBoneTransforms,
               GUILayout.Width(60)
               );
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            _extractBlendShapes = EditorGUILayout.ToggleLeft(
               new GUIContent("Extract Blend Shapes", "Extracts the blend shapes of the source model." +
               "\n\nNOTICE: This also means it will NOT bake the current pose of the mesh. Instead it will export the default pose." +
               "\n\nThe expectation is that you will use this on the same animator setup (aka 'Rig' or 'Armature') you exported it from. " +
               "It will most likely NOT work on another rig. Please read up on skinning and rigging meshes if you are not sure what this means." +
               "\n\nBlend shape information is NOT saved in .obj files (it's just not supported by that file format)."
                ),
                _extractBlendShapes,
                GUILayout.Width(160)
            );

            GUILayout.BeginHorizontal();
            GUI.enabled = !_extractBoneWeights;
            GUILayout.Label("Pivot:", GUILayout.Width(50));
            string pivotMsg = _extractBoneWeights ? "\n\nPivot modifications will be ignored if bone weights are exported. So it makes not sense to allow this." : "";
            if (GUILayout.Button(new GUIContent("Center", "Centers the pivot relative to all selected vertices.\nNOTICE: The rotation is always aligned to world space." + pivotMsg)))
            {
                MeshExtractorTool.Instance.CenterPivot();
                _pivotModified = false;
                _pivotBehaviour = PivotBehaviour.Center;
            }
            if (GUILayout.Button(new GUIContent("Origin", "Sets the pivot to 0/0/0 in the local transform of the currently selected object.\nNOTICE: The rotation is always aligned to world space." + pivotMsg)))
            {
                MeshExtractorTool.Instance.ResetPivotToOrigin();
                _pivotModified = false;
                _pivotBehaviour = PivotBehaviour.Origin;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Extract Mesh"))
            {
                MeshExtractorTool.Instance.Extract(
                    _newMeshName, _replaceOldMesh, _preserveSubMeshes, _combineSubMeshesBasedOnMaterials, _combineMeshes, _saveAsObj, _extractTextures,
                    _extractBoneWeights, _extractBoneTransforms,
                    _extractBlendShapes
                    );
            }
        }


#region GUI Helpers
        public static bool DrawButton(string text, string tooltip = null, string icon = null, GUIStyle style = null, params GUILayoutOption[] options)
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

            if (style == null)
                style = new GUIStyle(GUI.skin.button);

            return GUILayout.Button(content, style, options);
        }

        public static void BeginHorizontalIndent(int indentAmount = 10, bool beginVerticalInside = true)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(indentAmount);

            if (beginVerticalInside)
                GUILayout.BeginVertical();
        }

        public static void EndHorizontalIndent(float indentAmount = 10, bool begunVerticalInside = true, bool bothSides = false)
        {
            if (begunVerticalInside)
                GUILayout.EndVertical();

            if (bothSides)
                GUILayout.Space(indentAmount);

            GUILayout.EndHorizontal();
        }

        public static void DrawLabel(string text, string tooltip = null, Color? color = null, bool bold = false, bool wordwrap = true, bool richText = true, Texture icon = null, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (!color.HasValue)
                color = GUI.skin.label.normal.textColor;

            if (style == null)
                style = new GUIStyle(GUI.skin.label);
            if (bold)
                style.fontStyle = FontStyle.Bold;
            else
                style.fontStyle = FontStyle.Normal;

            style.normal.textColor = color.Value;
            style.hover.textColor = color.Value;
            style.wordWrap = wordwrap;
            style.richText = richText;
            style.imagePosition = ImagePosition.ImageLeft;

            var content = new GUIContent(text);
            if (tooltip != null)
                content.tooltip = tooltip;
            if (icon != null)
            {
                GUILayout.Space(16);
                var position = GUILayoutUtility.GetRect(content, style, options);
                GUI.DrawTexture(new Rect(position.x - 16, position.y, 16, 16), icon);
                GUI.Label(position, content, style);
            }
            else
            {
                GUILayout.Label(content, style, options);
            }
        }
#endregion
    }
}
