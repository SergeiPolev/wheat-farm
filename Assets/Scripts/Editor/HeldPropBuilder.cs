using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WheatFarm.Editor
{
    /// <summary>
    /// Builds the low-poly tool props (watering can, seed bag, paint bucket, sickle) from primitives,
    /// saves them as prefabs and mounts them on a socket under the farmer's RightHand bone in Player.prefab,
    /// wired into HeldToolView. Re-running rebuilds everything, so grip offsets live here as constants.
    ///
    /// Prop convention: origin = grip point, local +Y = prop "up", local X = the bar the fingers close around.
    /// </summary>
    public static class HeldPropBuilder
    {
        private const string PlayerPrefab = "Assets/Project/Prefabs/Player/Player.prefab";
        private const string PropDir = "Assets/Project/Prefabs/Player/Props";
        private const string MatDir = "Assets/Project/Materials/Props";
        private const string SocketName = "ToolSocket";
        private const string ViewType = "WheatFarm.Player.HeldTools.HeldToolView, WheatFarm.Player";

        // Props are oversized for readability from the isometric camera (the farmer is ~130 px tall on a 1080p screen).
        internal const float PropScale = 1.7f;

        // Grip pose of each prop in socket space (socket = RightHand with bone scale cancelled, units = world).
        // Tuned against the Quaternius idle/watering/harvest poses. Socket +Z points away from the body in idle/walk,
        // which is also the grip bar axis: props slide 0.07 out along the bar (fist stays on it) to clear the thigh.
        internal static readonly Dictionary<string, (Vector3 pos, Vector3 euler)> Grip = new()
        {
            ["WateringCan"] = (new Vector3(0f, 0.045f, 0.07f), new Vector3(180f, 90f, 0f)),
            ["SeedBag"] = (new Vector3(0f, 0.045f, 0.03f), new Vector3(180f, 90f, 0f)),
            ["PaintBucket"] = (new Vector3(0f, 0.045f, 0.07f), new Vector3(180f, 90f, 0f)),
            ["Sickle"] = (new Vector3(0f, 0.045f, 0.01f), new Vector3(270f, 180f, 0f)), // blade hooks forward-down
        };

        [MenuItem("WheatFarm/Build Held Tool Props")]
        public static void Build()
        {
            EnsureFolder(PropDir);
            EnsureFolder(MatDir);

            var mats = new Dictionary<string, Material>
            {
                ["Teal"] = Mat("Prop_Teal", new Color(0.27f, 0.62f, 0.56f), 0.35f, 0.45f),
                ["Steel"] = Mat("Prop_Steel", new Color(0.72f, 0.74f, 0.77f), 0.6f, 0.6f),
                ["DarkSteel"] = Mat("Prop_DarkSteel", new Color(0.35f, 0.36f, 0.39f), 0.5f, 0.5f),
                ["Burlap"] = Mat("Prop_Burlap", new Color(0.74f, 0.6f, 0.4f), 0f, 0.15f),
                ["Twine"] = Mat("Prop_Twine", new Color(0.47f, 0.33f, 0.2f), 0f, 0.2f),
                ["Wood"] = Mat("Prop_Wood", new Color(0.55f, 0.36f, 0.2f), 0f, 0.3f),
                ["Paint"] = Mat("Prop_Paint", new Color(0.86f, 0.3f, 0.32f), 0f, 0.75f),
            };

            var prefabs = new Dictionary<string, GameObject>
            {
                ["WateringCan"] = SavePrefab(BuildWateringCan(mats)),
                ["SeedBag"] = SavePrefab(BuildSeedBag(mats)),
                ["PaintBucket"] = SavePrefab(BuildPaintBucket(mats)),
                ["Sickle"] = SavePrefab(BuildSickle(mats)),
            };

            MountOnPlayer(prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[HeldPropBuilder] Props built and mounted on " + PlayerPrefab);
        }

        [MenuItem("WheatFarm/Render Held Prop Sheet")]
        public static void RenderSheetMenu() => RenderSheet("held_props.png");

        /// <summary>
        /// Renders every prop on the farmer across idle/walk/action poses into Temp/Screens/&lt;fileName&gt;.
        /// Rows: per prop a full-body view and a hand close-up. Columns: poses.
        /// </summary>
        public static string RenderSheet(string fileName)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var graph = UnityEngine.Playables.PlayableGraph.Create("HeldPropSheet");
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), scene);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;

                var light = new GameObject("Light").AddComponent<Light>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional; light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(40f, 200f, 0f);

                var cam = new GameObject("Cam").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
                cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.62f, 0.5f); cam.fieldOfView = 30f; cam.nearClipPlane = 0.01f;

                var anim = go.GetComponentInChildren<Animator>(true);
                anim.runtimeAnimatorController = null;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // otherwise bones are not written until the renderer is seen
                var smr = anim.GetComponentInChildren<SkinnedMeshRenderer>(true);
                smr.forceMatrixRecalculationPerRender = true; // skin follows the sampled pose in a preview scene
                smr.updateWhenOffscreen = true;
                var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                var socket = hand.Find(SocketName);

                AnimationClip Clip(string fbx, string n)
                {
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"Assets/Project/Animations/Quaternius/{fbx}.fbx"))
                        if (o is AnimationClip c && c.name == n) return c;
                    throw new InvalidOperationException("clip " + n);
                }
                var poses = new (AnimationClip clip, float t)[]
                {
                    (Clip("UAL1_Standard", "Idle_Loop"), 0.2f), (Clip("UAL1_Standard", "Walk_Loop"), 0.3f),
                    (Clip("UAL2_Standard", "Farm_Watering"), 0.45f), (Clip("UAL2_Standard", "Farm_Harvest"), 0.5f),
                    (Clip("UAL2_Standard", "Farm_PlantSeed"), 0.5f), (Clip("UAL2_Standard", "OverhandThrow"), 0.3f),
                };

                const int cell = 200;
                int rows = socket.childCount * 2;
                var sheet = new Texture2D(cell * poses.Length, cell * rows, TextureFormat.RGB24, false);
                var rt = new RenderTexture(cell, cell, 24);
                cam.targetTexture = rt;
                var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", anim);
                graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                // Warm-up: the very first evaluation of a fresh graph leaves the bind pose.
                var warm = UnityEngine.Animations.AnimationClipPlayable.Create(graph, poses[0].clip);
                UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, warm);
                graph.Evaluate(); graph.Evaluate();

                for (int pi = 0; pi < socket.childCount; pi++)
                {
                    for (int i = 0; i < socket.childCount; i++) socket.GetChild(i).gameObject.SetActive(i == pi);
                    for (int k = 0; k < poses.Length; k++)
                    {
                        var cp = UnityEngine.Animations.AnimationClipPlayable.Create(graph, poses[k].clip);
                        cp.SetApplyFootIK(false);
                        UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, cp);
                        UnityEngine.Playables.PlayableExtensions.SetTime(cp, poses[k].clip.length * poses[k].t);
                        graph.Evaluate(); graph.Evaluate(); // first evaluate after a source change can leave the bind pose

                        for (int v = 0; v < 2; v++)
                        {
                            // The farmer faces +Z: views are from the front-right.
                            var center = v == 0 ? new Vector3(0f, 0.9f, 0f) : hand.position;
                            var dir = v == 0 ? new Vector3(0.8f, 0.35f, 1f).normalized : new Vector3(1f, 0.5f, 0.8f).normalized;
                            cam.transform.position = center + dir * (v == 0 ? 4.5f : 1.0f);
                            cam.transform.LookAt(center);
                            cam.Render();
                            RenderTexture.active = rt;
                            var tex = new Texture2D(cell, cell, TextureFormat.RGB24, false);
                            tex.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                            tex.Apply();
                            sheet.SetPixels(k * cell, (rows - 1 - (pi * 2 + v)) * cell, cell, cell, tex.GetPixels());
                            UnityEngine.Object.DestroyImmediate(tex);
                        }
                        UnityEngine.Playables.PlayableExtensions.Destroy(cp);
                    }
                }
                RenderTexture.active = null; cam.targetTexture = null; rt.Release();
                var dir2 = System.IO.Path.Combine(Application.dataPath, "../Temp/Screens");
                System.IO.Directory.CreateDirectory(dir2);
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir2, fileName));
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------- props ----------

        private static GameObject BuildWateringCan(Dictionary<string, Material> m)
        {
            var root = new GameObject("Prop_WateringCan");
            // Handle bar across the top (grip), two posts down to the body.
            Part(root, PrimitiveType.Cube, "Handle", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.11f, 0.018f, 0.018f), m["Teal"]);
            Part(root, PrimitiveType.Cube, "PostBack", new Vector3(-0.05f, -0.03f, 0f), Vector3.zero, new Vector3(0.016f, 0.06f, 0.016f), m["Teal"]);
            Part(root, PrimitiveType.Cube, "PostFront", new Vector3(0.045f, -0.03f, 0f), Vector3.zero, new Vector3(0.016f, 0.06f, 0.016f), m["Teal"]);
            Part(root, PrimitiveType.Cylinder, "Body", new Vector3(0f, -0.115f, 0f), Vector3.zero, new Vector3(0.14f, 0.06f, 0.14f), m["Teal"]);
            Part(root, PrimitiveType.Cylinder, "Rim", new Vector3(0f, -0.055f, 0f), Vector3.zero, new Vector3(0.145f, 0.006f, 0.145f), m["DarkSteel"]);
            // Spout from the front of the body, angled up, with a rose at the tip.
            Part(root, PrimitiveType.Cylinder, "Spout", new Vector3(0.115f, -0.1f, 0f), new Vector3(0f, 0f, -55f), new Vector3(0.022f, 0.065f, 0.022f), m["Teal"]);
            Part(root, PrimitiveType.Cylinder, "Rose", new Vector3(0.165f, -0.063f, 0f), new Vector3(0f, 0f, -55f), new Vector3(0.042f, 0.01f, 0.042f), m["DarkSteel"]);
            return root;
        }

        private static GameObject BuildSeedBag(Dictionary<string, Material> m)
        {
            var root = new GameObject("Prop_SeedBag");
            Part(root, PrimitiveType.Cylinder, "Neck", new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(0.045f, 0.022f, 0.045f), m["Burlap"]);
            Part(root, PrimitiveType.Cylinder, "Tie", new Vector3(0f, -0.03f, 0f), Vector3.zero, new Vector3(0.052f, 0.007f, 0.052f), m["Twine"]);
            Part(root, PrimitiveType.Sphere, "Sack", new Vector3(0f, -0.11f, 0f), Vector3.zero, new Vector3(0.14f, 0.16f, 0.12f), m["Burlap"]);
            Part(root, PrimitiveType.Sphere, "Tuft", new Vector3(0f, 0.014f, 0f), Vector3.zero, new Vector3(0.06f, 0.025f, 0.05f), m["Burlap"]);
            return root;
        }

        private static GameObject BuildPaintBucket(Dictionary<string, Material> m)
        {
            var root = new GameObject("Prop_PaintBucket");
            // Wire bail: grip bar plus two drops to the rim.
            Part(root, PrimitiveType.Cube, "Bail", new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.12f, 0.01f, 0.01f), m["DarkSteel"]);
            Part(root, PrimitiveType.Cube, "BailL", new Vector3(-0.06f, -0.025f, 0f), Vector3.zero, new Vector3(0.008f, 0.05f, 0.008f), m["DarkSteel"]);
            Part(root, PrimitiveType.Cube, "BailR", new Vector3(0.06f, -0.025f, 0f), Vector3.zero, new Vector3(0.008f, 0.05f, 0.008f), m["DarkSteel"]);
            Part(root, PrimitiveType.Cylinder, "Bucket", new Vector3(0f, -0.105f, 0f), Vector3.zero, new Vector3(0.12f, 0.055f, 0.12f), m["Steel"]);
            Part(root, PrimitiveType.Cylinder, "Paint", new Vector3(0f, -0.059f, 0f), Vector3.zero, new Vector3(0.11f, 0.004f, 0.11f), m["Paint"]);
            return root;
        }

        private static GameObject BuildSickle(Dictionary<string, Material> m)
        {
            var root = new GameObject("Prop_Sickle");
            // Handle along local Y through the fist, blade curving forward (+Z) from its top end.
            Part(root, PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.01f, 0f), Vector3.zero, new Vector3(0.026f, 0.065f, 0.026f), m["Wood"]);
            Part(root, PrimitiveType.Cylinder, "Ferrule", new Vector3(0f, 0.078f, 0f), Vector3.zero, new Vector3(0.03f, 0.006f, 0.03f), m["DarkSteel"]);
            const int segments = 7;
            const float radius = 0.085f;
            var center = new Vector3(0f, 0.08f, radius);
            for (int i = 0; i < segments; i++)
            {
                // Arc from the handle top (angle 180°) over the top to the tip (~400°), in the YZ plane.
                float a0 = Mathf.Deg2Rad * (180f - i * 32f);
                float a1 = Mathf.Deg2Rad * (180f - (i + 1) * 32f);
                var p0 = center + new Vector3(0f, Mathf.Sin(a0) * radius * -1f, Mathf.Cos(a0) * radius);
                var p1 = center + new Vector3(0f, Mathf.Sin(a1) * radius * -1f, Mathf.Cos(a1) * radius);
                var mid = (p0 + p1) * 0.5f;
                var dir = p1 - p0;
                float width = Mathf.Lerp(0.03f, 0.012f, i / (float)(segments - 1));
                var seg = Part(root, PrimitiveType.Cube, "Blade" + i, mid, Vector3.zero, new Vector3(0.012f, width, dir.magnitude * 1.08f), m["Steel"]);
                seg.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.right) * Quaternion.Euler(0f, 0f, 90f);
            }
            return root;
        }

        // ---------- mounting ----------

        private static void MountOnPlayer(Dictionary<string, GameObject> prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                var hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (hand == null) hand = FindDeep(root.transform, "RightHand");
                if (hand == null) throw new InvalidOperationException("RightHand bone not found in " + PlayerPrefab);

                var old = hand.Find(SocketName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

                var socket = new GameObject(SocketName).transform;
                socket.SetParent(hand, false);
                // Cancel the bone's centimetre scale so prop sizes are in world units.
                socket.localScale = Vector3.one / hand.lossyScale.x * root.transform.lossyScale.x;

                var mounted = new Dictionary<string, GameObject>();
                foreach (var kv in prefabs)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(kv.Value, socket);
                    var (pos, euler) = Grip[kv.Key];
                    inst.transform.localPosition = pos;
                    inst.transform.localRotation = Quaternion.Euler(euler);
                    inst.transform.localScale = Vector3.one * PropScale;
                    inst.SetActive(false);
                    mounted[kv.Key] = inst;
                }

                var viewType = Type.GetType(ViewType);
                if (viewType == null) throw new InvalidOperationException("HeldToolView type not found");
                var view = animator.GetComponent(viewType) ?? animator.gameObject.AddComponent(viewType);
                var so = new SerializedObject(view);
                so.FindProperty("_wateringCan").objectReferenceValue = mounted["WateringCan"];
                so.FindProperty("_seedBag").objectReferenceValue = mounted["SeedBag"];
                so.FindProperty("_paintBucket").objectReferenceValue = mounted["PaintBucket"];
                so.FindProperty("_sickle").objectReferenceValue = mounted["Sickle"];
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- helpers ----------

        private static GameObject Part(GameObject parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static GameObject SavePrefab(GameObject go)
        {
            var path = $"{PropDir}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
