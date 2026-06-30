using UnityEngine;
using UnityEditor;

namespace RedOniTools
{
    public class EdgeDecalGeneratorWindow : EditorWindow
    {
        const string LINK_TG = "https://t.me/redonigames";
        const string LINK_YOUTUBE = "https://www.youtube.com/@RedOniGamesDev";
        public EdgeDecalGenerator.LightingMode lightingMode = EdgeDecalGenerator.LightingMode.Flat;
        public float width = 0.04f;
        public float zOffset = 0.001f;
        public float hardEdgeAngle = 25f;
        public float textureWorldSize = 1f;
        public int trimCount = 1;
        public int trimIndex = 0;
        public float wingSnapThreshold = 0.02f;
        public Material decalMaterial;
        public bool createComponent = true;
        private bool saveMeshAsAsset = false;
        private Vector2 scrollPos;
        public bool useRandomUOffset = true;
        public float uOffsetRange = 10f;

        [MenuItem("Tools/RedOniTools/Edge Decal Generator")]
        public static void ShowWindow() => GetWindow<EdgeDecalGeneratorWindow>("Edge Decal Master");

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            bool changed = false;
            EditorGUILayout.LabelField("Red Oni Tools — Edge Decal Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            decalMaterial = (Material)EditorGUILayout.ObjectField("Decal Material", decalMaterial, typeof(Material), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            width = EditorGUILayout.FloatField("Width", width);
            zOffset = EditorGUILayout.Slider("Z Offset", zOffset, 0.0001f, 0.01f);
            hardEdgeAngle = EditorGUILayout.Slider("Hard Edge Angle", hardEdgeAngle, 1f, 179f);
            wingSnapThreshold = EditorGUILayout.FloatField("Wing Snap Threshold", wingSnapThreshold);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("UV / Texel Density", EditorStyles.boldLabel);
            textureWorldSize = EditorGUILayout.FloatField("Texture World Size", textureWorldSize);
            EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
            useRandomUOffset = EditorGUILayout.Toggle("Use Random U Offset", useRandomUOffset);
            if (useRandomUOffset)
            {
                uOffsetRange = EditorGUILayout.FloatField("U Offset Range", uOffsetRange);
            }
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Trim Atlas", EditorStyles.miniBoldLabel);
            trimCount = Mathf.Max(1, EditorGUILayout.IntField("Trim Count", trimCount));
            trimIndex = EditorGUILayout.IntSlider("Trim Index", trimIndex, 0, trimCount - 1);

            float previewVOffset = (float)(trimCount - 1 - Mathf.Clamp(trimIndex, 0, trimCount - 1)) / Mathf.Max(1, trimCount);
            float previewVScale = 1f / Mathf.Max(1, trimCount);
            EditorGUILayout.HelpBox($"V Offset: {previewVOffset:F4}   V Scale: {previewVScale:F4}", MessageType.None);
            
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            lightingMode = (EdgeDecalGenerator.LightingMode)EditorGUILayout.EnumPopup("Lighting Mode", lightingMode);
            saveMeshAsAsset = EditorGUILayout.Toggle(new GUIContent("Save Mesh As Asset", "Если включено, меш будет сохранен как .asset файл в Assets/GeneratedEdges"), saveMeshAsAsset);
            createComponent = EditorGUILayout.Toggle("Create Component", createComponent);
            EditorGUILayout.Space(8);

            GameObject[] targets = Selection.gameObjects;
            bool hasSelection = targets != null && targets.Length > 0;

            if (!hasSelection) EditorGUILayout.HelpBox("Select objects in the Scene.", MessageType.Info);

            GUI.enabled = hasSelection;
            if (GUILayout.Button("Generate Edge Decals (" + (hasSelection ? targets.Length : 0) + ")", GUILayout.Height(40)))
            {
                RunBatchGeneration(targets);
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;

            if (decalMaterial != null)
            {
                Texture2D preview = FindPreviewTexture(decalMaterial);

                if (preview != null)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("Texture Preview  (click to select trim)", EditorStyles.boldLabel);

                    float aspect = (float)preview.width / Mathf.Max(1, preview.height);
                    float maxW = EditorGUIUtility.currentViewWidth - 32f;
                    float boxW = Mathf.Min(maxW, preview.width);
                    float boxH = Mathf.Clamp(boxW / aspect, 40f, 220f);
                    if (boxW / aspect > 220f) boxW = 220f * aspect;

                    float offsetX = (maxW - boxW) * 0.5f;
                    Rect rect = GUILayoutUtility.GetRect(maxW, boxH);
                    rect = new Rect(rect.x + offsetX, rect.y, boxW, boxH);

                    EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.StretchToFill);

                    int tc = Mathf.Max(1, trimCount);
                    float trimH = rect.height / tc;

                    for (int i = 0; i < tc; i++)
                    {
                        float y = rect.y + i * trimH;
                        var r = new Rect(rect.x, y, rect.width, trimH);
                        Handles.DrawSolidRectangleWithOutline(r, Color.clear, new Color(1f, 1f, 1f, 0.5f));
                    }

                    int selIdx = Mathf.Clamp(trimIndex, 0, tc - 1);
                    float selY = rect.y + selIdx * trimH;
                    var selR = new Rect(rect.x, selY, rect.width, trimH);
                    Handles.DrawSolidRectangleWithOutline(
                        selR,
                        new Color(1f, 0.5f, 0f, 0.18f),
                        new Color(1f, 0.4f, 0f, 1f));

                    Event e = Event.current;
                    if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                    {
                        float relY = (e.mousePosition.y - rect.y) / rect.height;
                        int clicked = Mathf.Clamp(Mathf.FloorToInt(relY * tc), 0, tc - 1);
                        if (clicked != trimIndex)
                        {
                            trimIndex = clicked;
                            changed = true;
                        }
                        e.Use();
                    }
                }
            }

            DrawSocialLinks();
            EditorGUILayout.Space(20);
            EditorGUILayout.EndScrollView();
        }
        private void RunBatchGeneration(GameObject[] targets)
        {
            int count = 0;
            foreach (GameObject go in targets)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                EdgeDecalGenerator gen = go.GetComponent<EdgeDecalGenerator>();
                bool isNewComponent = false;
                if (gen == null)
                {
                    gen = go.AddComponent<EdgeDecalGenerator>();
                    isNewComponent = true;
                }

                gen.sourceMeshFilter = mf;
                gen.width = width;
                gen.zOffset = zOffset;
                gen.hardEdgeAngle = hardEdgeAngle;
                gen.textureWorldSize = textureWorldSize;
                gen.trimCount = trimCount;
                gen.trimIndex = trimIndex;
                gen.wingSnapThreshold = wingSnapThreshold;
                gen.decalMaterial = decalMaterial;
                gen.saveMeshAsAsset = saveMeshAsAsset;
                gen.lightingMode = lightingMode;
                gen.useRandomUOffset = useRandomUOffset;
                gen.uOffsetRange = uOffsetRange;
                gen.Generate();

                string decalName = $"{mf.sharedMesh.name}__EdgeDecal__";
                Transform decalChild = go.transform.Find(decalName);
                if (decalChild != null)
                {
                    Undo.RegisterCreatedObjectUndo(decalChild.gameObject, "Generate Edge Decal");
                }

                if (isNewComponent && !createComponent)
                {
                    DestroyImmediate(gen);
                }

                count++;
            }
            Debug.Log($"[EdgeDecal] Generated for {count} objects.");
        }
        private void DrawSocialLinks()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Telegram"))
                Application.OpenURL(LINK_TG);
            if (GUILayout.Button("▶  YouTube"))
                Application.OpenURL(LINK_YOUTUBE);
            EditorGUILayout.EndHorizontal();
        }

        private void OnSelectionChange() => Repaint();

        static Texture2D FindPreviewTexture(Material mat)
        {
            string[] names = { "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo" };
            foreach (var n in names) if (mat.HasProperty(n) && mat.GetTexture(n) is Texture2D t) return t;
            return null;
        }
    }
}