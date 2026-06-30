using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RedOniTools
{
    
    [ExecuteInEditMode]
    public class EdgeDecalGenerator : MonoBehaviour
    {
        [Header("Source")]
        public MeshFilter sourceMeshFilter;
        public enum LightingMode { Flat, Smooth, Hard }
        [Header("Profile")]
        public float width = 0.04f;
        [Range(0.0001f, 0.01f)]
        public float zOffset = 0.001f;
        [Range(1f, 179f)]
        public float hardEdgeAngle = 25f;

        [Header("UV")]
        public float textureWorldSize = 1f;
        [Min(1)]
        public int trimCount = 1;
        [Min(0)]
        public int trimIndex = 0;
        [Header("Randomization")]
        public bool useRandomUOffset = true;
        public float uOffsetRange = 10f;
        [Header("Wing Snap")]
        public float wingSnapThreshold = 0.02f;

        [Header("Output")]
        public Material decalMaterial;
        public LightingMode lightingMode = LightingMode.Flat;
        public bool saveMeshAsAsset = false;
        private struct HardEdge
        {
            public int cv0, cv1;
            public Vector3 p0, p1;
            public Vector3 nA, nB;
            public Vector3 ifA, ifB;
            public Vector3 avgN;
            public bool concave;
            public Vector3 start, end;
            public float fullLen;
        }

        private struct ApexInfo
        {
            public int vIdxA; 
            public int vIdxB; 
        }
        private Dictionary<(int, int), ApexInfo> _apexInfo;

        private struct WingInfo
        {
            public int vIdxA;
            public int vIdxB;
            public Vector3 dirAway;
        }
        private Dictionary<(int, int), WingInfo> _wingInfo;

        private List<int> _vertexToSourceMap = new List<int>();
        private List<int[]> _stripVertexIndices = new List<int[]>();

#if UNITY_EDITOR
        [ContextMenu("Generate Edge Decal Mesh")]
        public void GenerateFromContextMenu() => Generate();
#endif

        private void Reset()
        {
            if (sourceMeshFilter == null)
                sourceMeshFilter = GetComponent<MeshFilter>();
        }

        public void Generate()
        {
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                Debug.LogError("Assign Source MeshFilter.");
                return;
            }
            

            string meshName = sourceMeshFilter.sharedMesh.name;
            string targetName = $"{meshName}__EdgeDecal__";

            Transform existing = transform.Find(targetName);
            if (existing != null)
            {
                MeshFilter oldMf = existing.GetComponent<MeshFilter>();
                if (oldMf != null && oldMf.sharedMesh != null)
                {
#if UNITY_EDITOR
                    if (!AssetDatabase.Contains(oldMf.sharedMesh))
                        DestroyImmediate(oldMf.sharedMesh);
#endif
                }
                DestroyImmediate(existing.gameObject);
            }

            Mesh m = BuildDecalMesh(sourceMeshFilter.sharedMesh);
            GameObject go = new GameObject(targetName); 

            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
#if UNITY_EDITOR
            if (saveMeshAsAsset)
            {
                m = SaveMeshAsset(m, meshName);
            }
#endif
            mf.sharedMesh = m;
            mr.sharedMaterial = decalMaterial;

#if UNITY_EDITOR
            EditorUtility.SetDirty(go);
            if (GetComponent<EdgeDecalGenerator>() != null)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }
        private Mesh BuildDecalMesh(Mesh src)
        {
            _apexInfo = new Dictionary<(int, int), ApexInfo>();
            _wingInfo = new Dictionary<(int, int), WingInfo>();
            _vertexToSourceMap.Clear();
            _stripVertexIndices.Clear();

            var sv = src.vertices;
            var st = src.triangles;
            int tc = st.Length / 3;
            Vector3[] sn = src.normals;

            if (sn == null || sn.Length == 0)
            {
                Mesh calcMesh = new Mesh();
                calcMesh.vertices = sv;
                calcMesh.triangles = st;
                calcMesh.RecalculateNormals();
                sn = calcMesh.normals;
                DestroyImmediate(calcMesh);
            }

            var faceN = new Vector3[tc];
            var faceCent = new Vector3[tc];
            for (int i = 0; i < tc; i++)
            {
                Vector3 a = sv[st[i * 3]], b = sv[st[i * 3 + 1]], c = sv[st[i * 3 + 2]];
                faceN[i] = Vector3.Cross(b - a, c - a).normalized;
                faceCent[i] = (a + b + c) / 3f;
            }

            int[] pi = WeldVerts(sv, out Dictionary<int, Vector3> weldedNormals, sn);
            var cps = new Dictionary<int, Vector3>();
            for (int i = 0; i < sv.Length; i++) cps[pi[i]] = sv[i];

            var edgeFaces = new Dictionary<(int, int), (int, int)>();
            for (int fi = 0; fi < tc; fi++)
                for (int e = 0; e < 3; e++)
                {
                    int a = pi[st[fi * 3 + e]], b = pi[st[fi * 3 + (e + 1) % 3]];
                    var k = a < b ? (a, b) : (b, a);
                    if (!edgeFaces.TryGetValue(k, out var p)) edgeFaces[k] = (fi, -1);
                    else if (p.Item2 < 0) edgeFaces[k] = (p.Item1, fi);
                }

            float cosT = Mathf.Cos(hardEdgeAngle * Mathf.Deg2Rad);
            var edges = new List<HardEdge>();

            foreach (var kv in edgeFaces)
            {
                var (fA, fB) = kv.Value;
                if (fB < 0) continue;
                if (Vector3.Dot(faceN[fA], faceN[fB]) >= cosT) continue;

                Vector3 p0 = cps[kv.Key.Item1], p1 = cps[kv.Key.Item2];
                Vector3 dir = (p1 - p0).normalized, mid = (p0 + p1) * 0.5f;
                Vector3 nA = faceN[fA], nB = faceN[fB];

                Vector3 ifA = Vector3.Cross(nA, dir).normalized;
                Vector3 ifB = Vector3.Cross(dir, nB).normalized;
                if (Vector3.Dot(ifA, faceCent[fA] - mid) < 0f) ifA = -ifA;
                if (Vector3.Dot(ifB, faceCent[fB] - mid) < 0f) ifB = -ifB;

                Vector3 avgN = (weldedNormals[kv.Key.Item1] + weldedNormals[kv.Key.Item2]).normalized;
                Vector3 testPt = mid + avgN * width;
                bool isConcave = Vector3.Dot(testPt - faceCent[fA], nA) < 0f || Vector3.Dot(testPt - faceCent[fB], nB) < 0f;

                edges.Add(new HardEdge
                {
                    cv0 = kv.Key.Item1,
                    cv1 = kv.Key.Item2,
                    p0 = p0,
                    p1 = p1,
                    nA = nA,
                    nB = nB,
                    ifA = ifA,
                    ifB = ifB,
                    avgN = avgN,
                    concave = isConcave,
                    fullLen = Vector3.Distance(p0, p1)
                });
            }

            if (edges.Count == 0) return new Mesh();

            var vertEdges = new Dictionary<int, List<int>>();
            for (int i = 0; i < edges.Count; i++) { AddL(vertEdges, edges[i].cv0, i); AddL(vertEdges, edges[i].cv1, i); }

            var miterOff = new Dictionary<(int, int), float>();
            for (int i = 0; i < edges.Count; i++)
            {
                CalcMiter(i, edges[i].cv0, edges, vertEdges, miterOff);
                CalcMiter(i, edges[i].cv1, edges, vertEdges, miterOff);
            }
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                var dir = (e.p1 - e.p0).normalized;
                float off0 = GetM(miterOff, i, e.cv0), off1 = GetM(miterOff, i, e.cv1);
                float total = off0 + off1;
                if (total > e.fullLen * 0.9f) { float s = e.fullLen * 0.9f / total; off0 *= s; off1 *= s; }
                e.start = e.p0 + dir * off0; e.end = e.p1 - dir * off1;
                edges[i] = e;
            }

            var outV = new List<Vector3>();
            var outN = new List<Vector3>();
            var outU = new List<Vector2>();
            var outT = new List<int>();

            for (int ei = 0; ei < edges.Count; ei++)
            {
                if (Vector3.Distance(edges[ei].start, edges[ei].end) < 1e-5f) continue;
                EmitStrip(ei, edges[ei], outV, outN, outU, outT);
            }

            ApplySnappingGeometry(vertEdges, edges, outV, outN, cps);
            ApplyWorldSpaceUVs(outV, outN, outU);
            for (int i = 0; i < outV.Count; i++)
            {
                int sourceIdx = _vertexToSourceMap[i];
                outV[i] += weldedNormals[sourceIdx] * zOffset;
            }

            var m = new Mesh { name = src.name + "_Decal" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(outV);
            m.SetNormals(outN);
            m.SetUVs(0, outU);
            m.SetTriangles(outT, 0);
            m.RecalculateBounds();
            return m;
        }
        private void EmitStrip(int ei, HardEdge e, List<Vector3> outV, List<Vector3> outN, List<Vector2> outU, List<int> outT)
        {
            int b = outV.Count;
            Vector3[] pts = {
                e.start + e.ifA * width, e.start, e.start, e.start + e.ifB * width, // Start side
                e.end + e.ifA * width,   e.end,   e.end,   e.end + e.ifB * width   // End side
            };
            Vector3 nA, nB;
            if (lightingMode == LightingMode.Flat)
            {
                nA = nB = e.avgN;
            }
            else if (lightingMode == LightingMode.Hard)
            {
                nA = e.nA; nB = e.nB;
            }
            else
            { // Smooth
                nA = nB = e.avgN;
            }

            Vector3[] norms = { nA, nA, nB, nB, nA, nA, nB, nB };

            for (int i = 0; i < 8; i++)
            {
                outV.Add(pts[i]);
                outN.Add(norms[i]);
                outU.Add(Vector2.zero);
                _vertexToSourceMap.Add(i < 4 ? e.cv0 : e.cv1);
            }
            for (int i = 0; i < 8; i++)
            {
                outV.Add(pts[i]);
                outN.Add(norms[i]);
                outU.Add(Vector2.zero);
                _vertexToSourceMap.Add(i < 4 ? e.cv0 : e.cv1);
            }          
            _stripVertexIndices.Add(new int[] { b, b + 1, b + 2, b + 3, b + 4, b + 5, b + 6, b + 7 });
            _apexInfo[(ei, e.cv0)] = new ApexInfo { vIdxA = b + 1, vIdxB = b + 2 };
            _apexInfo[(ei, e.cv1)] = new ApexInfo { vIdxA = b + 5, vIdxB = b + 6 };

            _wingInfo[(ei, e.cv0)] = new WingInfo { vIdxA = b + 0, vIdxB = b + 3, dirAway = (e.p1 - e.p0).normalized };
            _wingInfo[(ei, e.cv1)] = new WingInfo { vIdxA = b + 4, vIdxB = b + 7, dirAway = (e.p0 - e.p1).normalized };

            bool flip = Vector3.Dot(Vector3.Cross(pts[3] - pts[0], pts[4] - pts[0]).normalized, e.avgN) < 0f;

            int[] tris;
            if (flip)
            {
                tris = new int[] {
            0, 4, 1, 1, 4, 5, 
            2, 6, 3, 3, 6, 7  
        };
            }
            else
            {
                tris = new int[] {
            0, 1, 4, 1, 5, 4,
            2, 3, 6, 3, 7, 6  
        };
            }
            for (int i = 0; i < tris.Length; i++) outT.Add(b + tris[i]);
        }

        private void ApplyWorldSpaceUVs(List<Vector3> outV, List<Vector3> outN, List<Vector2> outU)
        {
            float vOff = (float)(trimCount - 1 - Mathf.Clamp(trimIndex, 0, trimCount - 1)) / trimCount;
            float vScl = 1f / Mathf.Max(1, trimCount);
            float vMid = vOff + 0.5f * vScl;
            float vHalfRange = 0.5f * vScl;

            for (int i = 0; i < _stripVertexIndices.Count; i++)
            {
                int[] ids = _stripVertexIndices[i];
                if (ids.Length < 8) continue;
                Vector3 pStart = outV[ids[1]];
                Vector3 pEnd = outV[ids[5]];
                Vector3 forward = (pEnd - pStart).normalized;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                float randomU = 0;
                if (useRandomUOffset)
                {
                    Random.State oldState = Random.state;
                    Random.InitState(i * 123);
                    randomU = Random.Range(0f, uOffsetRange);
                    Random.state = oldState;
                }
                for (int j = 0; j < 8; j++)
                {
                    int vIdx = ids[j];
                    Vector3 pos = outV[vIdx];
                    Vector3 relative = pos - pStart;

                    float uDist = Vector3.Dot(relative, forward);
                    float u = (uDist / Mathf.Max(0.001f, textureWorldSize)) + randomU;

                    Vector3 onLine = pStart + forward * uDist;
                    float distAcross = Vector3.Distance(pos, onLine);
                    Vector3 sideDir = Vector3.Cross(outN[vIdx], forward).normalized;
                    float sideSign = Mathf.Sign(Vector3.Dot(pos - onLine, sideDir));

                    float vPosNorm = (width > 1e-4f) ? (distAcross * sideSign / width) : sideSign;
                    float v = vMid + vPosNorm * vHalfRange;

                    outU[vIdx] = new Vector2(u, v);
                }
            }
        }      
        private void ApplySnappingGeometry(Dictionary<int, List<int>> vertEdges, List<HardEdge> edges, List<Vector3> outV, List<Vector3> outN, Dictionary<int, Vector3> cps)
        {
            foreach (var kv in vertEdges)
            {
                int cv = kv.Key; Vector3 vp = cps[cv];

                var allApexIdx = new List<int>();
                var apexNormals = new List<(int vIdx, Vector3 faceN)>();

                foreach (int ei in kv.Value)
                {
                    if (_apexInfo.TryGetValue((ei, cv), out var ai))
                    {
                        allApexIdx.Add(ai.vIdxA); allApexIdx.Add(ai.vIdxB);
                        apexNormals.Add((ai.vIdxA, edges[ei].nA));
                        apexNormals.Add((ai.vIdxB, edges[ei].nB));
                    }
                }

                if (allApexIdx.Count > 0)
                {
                    Vector3 snapPos = Vector3.zero;
                    int convexCnt = 0;
                    foreach (int ei in kv.Value)
                    {
                        if (_apexInfo.TryGetValue((ei, cv), out var ai) && !edges[ei].concave)
                        {
                            snapPos += vp; convexCnt++;
                        }
                    }
                    snapPos = (convexCnt > 0) ? snapPos / convexCnt : vp;
                    foreach (int idx in allApexIdx) outV[idx] = snapPos;

                    if (lightingMode == LightingMode.Smooth)
                    {
                        Vector3 avgN = Vector3.zero;
                        foreach (int idx in allApexIdx) avgN += outN[idx];
                        avgN = avgN.normalized;
                        foreach (int idx in allApexIdx) outN[idx] = avgN;
                    }
                    else if (lightingMode == LightingMode.Hard)
                    {
                        for (int i = 0; i < apexNormals.Count; i++)
                            for (int j = i + 1; j < apexNormals.Count; j++)
                                if (Vector3.Dot(apexNormals[i].faceN, apexNormals[j].faceN) > 0.99f)
                                {
                                    Vector3 n = (outN[apexNormals[i].vIdx] + outN[apexNormals[j].vIdx]).normalized;
                                    outN[apexNormals[i].vIdx] = n; outN[apexNormals[j].vIdx] = n;
                                }
                    }
                }

                var wings = new List<(int vIdx, Vector3 dir, Vector3 faceN)>();
                foreach (int ei in kv.Value)
                    if (_wingInfo.TryGetValue((ei, cv), out var wi))
                    {
                        wings.Add((wi.vIdxA, wi.dirAway, edges[ei].nA));
                        wings.Add((wi.vIdxB, wi.dirAway, edges[ei].nB));
                    }

                float thresh2 = wingSnapThreshold * wingSnapThreshold * 100f;
                for (int a = 0; a < wings.Count; a++)
                    for (int b = a + 1; b < wings.Count; b++)
                    {
                        if (Vector3.Dot(wings[a].faceN, wings[b].faceN) < 0.99f) continue;
                        Vector3 pA = outV[wings[a].vIdx], pB = outV[wings[b].vIdx];
                        float d1d2 = Vector3.Dot(wings[a].dir, wings[b].dir), denom = 1f - d1d2 * d1d2;
                        Vector3 res = (pA + pB) * 0.5f;
                        if (Mathf.Abs(denom) > 1e-6f)
                        {
                            float t = (Vector3.Dot(pB - pA, wings[a].dir) - d1d2 * Vector3.Dot(pB - pA, wings[b].dir)) / denom;
                            res = pA + wings[a].dir * t;
                        }
                        if ((res - pA).sqrMagnitude < thresh2 && (res - pB).sqrMagnitude < thresh2)
                        {
                            outV[wings[a].vIdx] = res; outV[wings[b].vIdx] = res;
                            if (lightingMode == LightingMode.Smooth)
                            {
                                Vector3 n = (outN[wings[a].vIdx] + outN[wings[b].vIdx]).normalized;
                                outN[wings[a].vIdx] = n; outN[wings[b].vIdx] = n;
                            }
                        }
                    }
            }
        }

        private void CalcMiter(int ei, int cv, List<HardEdge> edges,
            Dictionary<int, List<int>> ve, Dictionary<(int, int), float> mo)
        {
            if (!ve.TryGetValue(cv, out var nb)) return;
            var e0 = edges[ei];
            Vector3 d0 = cv == e0.cv0 ? (e0.p1 - e0.p0).normalized : (e0.p0 - e0.p1).normalized;
            float mx = 0f;
            foreach (int ni in nb)
            {
                if (ni == ei) continue;
                var en = edges[ni];
                Vector3 dn = cv == en.cv0 ? (en.p1 - en.p0).normalized : (en.p0 - en.p1).normalized;
                float cosA = Mathf.Clamp(Vector3.Dot(d0, dn), -1f, 1f);
                float tanH = Mathf.Tan(Mathf.Acos(cosA) * 0.5f);
                if (tanH < 1e-4f) continue;
                mx = Mathf.Max(mx, width / tanH);
            }
            if (mx > 0f) { var k = (ei, cv); if (!mo.ContainsKey(k) || mo[k] < mx) mo[k] = mx; }
        }
        private float GetM(Dictionary<(int, int), float> d, int e, int v) => d.TryGetValue((e, v), out float f) ? f : 0f;
        private void AddL(Dictionary<int, List<int>> d, int k, int v) { if (!d.TryGetValue(k, out var l)) d[k] = l = new List<int>(); if (!l.Contains(v)) l.Add(v); }

        private int[] WeldVerts(Vector3[] verts, out Dictionary<int, Vector3> normalsMap, Vector3[] normals, float g = 10000f)
        {
            var map = new Dictionary<Vector3Int, int>(); var res = new int[verts.Length];
            normalsMap = new Dictionary<int, Vector3>(); int cnt = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(verts[i].x * g), Mathf.RoundToInt(verts[i].y * g), Mathf.RoundToInt(verts[i].z * g));
                if (!map.TryGetValue(k, out int idx)) { map[k] = idx = cnt++; normalsMap[idx] = normals[i]; }
                else normalsMap[idx] = (normalsMap[idx] + normals[i]).normalized;
                res[i] = idx;
            }
            return res;
        }
#if UNITY_EDITOR
        private Mesh SaveMeshAsset(Mesh mesh, string meshName)
        {
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedEdges"))
                AssetDatabase.CreateFolder("Assets", "GeneratedEdges");

            string assetName = $"Edges_{gameObject.name}_{meshName}";
            string path = $"Assets/GeneratedEdges/{assetName}.asset";

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existingMesh != null)
            {
                existingMesh.Clear();
                EditorUtility.CopySerialized(mesh, existingMesh);
                DestroyImmediate(mesh); 
                AssetDatabase.SaveAssets();
                return existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.SaveAssets();
                return mesh;
            }
        }
#endif
    }
}

