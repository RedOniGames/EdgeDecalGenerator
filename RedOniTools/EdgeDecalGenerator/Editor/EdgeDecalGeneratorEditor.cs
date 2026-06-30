#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace RedOniTools
{
    [CustomEditor(typeof(EdgeDecalGenerator))]
    public class EdgeDecalGeneratorEditor : Editor
    {
        const string LINK_TG = "https://t.me/redonigames";
        const string LINK_YOUTUBE = "https://www.youtube.com/@RedOniGamesDev";

        public override void OnInspectorGUI()
        {
            var gen = (EdgeDecalGenerator)target;
            bool changed = false;

            EditorGUILayout.LabelField("Red Oni Tools — Edge Decal Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            var newMF = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", gen.sourceMeshFilter, typeof(MeshFilter), true);
            if (newMF != gen.sourceMeshFilter) { gen.sourceMeshFilter = newMF; changed = true; }
            gen.decalMaterial = (Material)EditorGUILayout.ObjectField("Decal Material", gen.decalMaterial, typeof(Material), false);         
          
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            gen.width = EditorGUILayout.FloatField("Width", gen.width);
            gen.zOffset = EditorGUILayout.Slider("Z Offset", gen.zOffset, 0.0001f, 0.01f);
            gen.hardEdgeAngle = EditorGUILayout.Slider("Hard Edge Angle", gen.hardEdgeAngle, 1f, 179f);
            gen.wingSnapThreshold = EditorGUILayout.FloatField("Wing Snap Threshold", gen.wingSnapThreshold);


            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("UV / Texel Density", EditorStyles.boldLabel);
            gen.textureWorldSize = EditorGUILayout.FloatField("Texture World Size", gen.textureWorldSize);
            EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
            gen.useRandomUOffset = EditorGUILayout.Toggle("Use Random U Offset", gen.useRandomUOffset);
            if (gen.useRandomUOffset)
            {
                gen.uOffsetRange = EditorGUILayout.FloatField("U Offset Range", gen.uOffsetRange);
            }
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Trim Atlas", EditorStyles.miniBoldLabel);

            int newTrimCount = Mathf.Max(1, EditorGUILayout.IntField("Trim Count", gen.trimCount));
            if (newTrimCount != gen.trimCount) { gen.trimCount = newTrimCount; changed = true; }

            int newTrimIndex = EditorGUILayout.IntSlider("Trim Index", gen.trimIndex, 0, Mathf.Max(0, gen.trimCount - 1));
            if (newTrimIndex != gen.trimIndex) { gen.trimIndex = newTrimIndex; changed = true; }

            float previewVOffset = (float)Mathf.Clamp(gen.trimIndex, 0, gen.trimCount - 1) / Mathf.Max(1, gen.trimCount);
            float previewVScale = 1f / Mathf.Max(1, gen.trimCount);
            EditorGUILayout.HelpBox($"V Offset: {previewVOffset:F4}   V Scale: {previewVScale:F4}", MessageType.None);

            
            
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            gen.lightingMode = (EdgeDecalGenerator.LightingMode)EditorGUILayout.EnumPopup("Lighting Mode", gen.lightingMode);
            gen.saveMeshAsAsset = EditorGUILayout.Toggle("Save Mesh As Asset", gen.saveMeshAsAsset);

            EditorGUILayout.Space(8);
            GUI.enabled = gen.sourceMeshFilter != null;
            if (GUILayout.Button("Generate Edge Decal Mesh", GUILayout.Height(32)))
            {
                gen.Generate();
                EditorUtility.SetDirty(gen);
            }
            GUI.enabled = true;

            if (gen.decalMaterial != null)
            {
                Texture2D preview = FindPreviewTexture(gen.decalMaterial);

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

                    int tc = Mathf.Max(1, gen.trimCount);
                    float trimH = rect.height / tc;

                    for (int i = 0; i < tc; i++)
                    {
                        float y = rect.y + i * trimH;
                        var r = new Rect(rect.x, y, rect.width, trimH);
                        Handles.DrawSolidRectangleWithOutline(r, Color.clear, new Color(1f, 1f, 1f, 0.5f));
                    }

                    int selIdx = Mathf.Clamp(gen.trimIndex, 0, tc - 1);
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
                        if (clicked != gen.trimIndex)
                        {
                            gen.trimIndex = clicked;
                            changed = true;
                        }
                        e.Use();
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Telegram"))
                Application.OpenURL(LINK_TG);
            if (GUILayout.Button("▶  YouTube"))
                Application.OpenURL(LINK_YOUTUBE);
            EditorGUILayout.EndHorizontal();

            if (changed) EditorUtility.SetDirty(gen);
        }

        static Texture2D FindPreviewTexture(Material mat)
        {
            if (mat.mainTexture is Texture2D t) return t;
            string[] names = { "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo", "_BaseColor" };
            foreach (var n in names)
                if (mat.HasProperty(n) && mat.GetTexture(n) is Texture2D found)
                    return found;
            return null;
        }
    }
}
#endif