#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WheatFarm.EditorTools
{
    /// <summary>
    /// Rebuilds FarmerAnimator with three layers:
    /// 1) Base Layer (NO MASK): Locomotion = nested 2D BlendTree that blends Walk and Run
    ///    9-direction BlendTrees by a Speed parameter (0=idle, 0.5=walk, 1=run).
    ///    Plays full-body rifle animations — upper body will be overwritten by layers above.
    /// 2) Upper Idle Layer (UpperBody mask, weight 1): plays an idle clip on the upper body so
    ///    arms don't hold a rifle (we don't want that pose from the rifle animations).
    /// 3) Actions Layer (UpperBody mask, weight 1): existing tool actions (Water/Plant/Harvest/etc),
    ///    overrides Upper Idle when ToolAction parameter is set.
    ///
    /// Why no mask on Base Layer? Unity's humanoid retargeting handles character orientation and
    /// root motion on the default layer — an avatar mask there breaks it.
    /// </summary>
    public static class FarmerAnimatorSetup
    {
        private const string ControllerPath = "Assets/Project/Animations/FarmerAnimator.controller";
        private const string UpperBodyMaskPath = "Assets/Project/Animations/UpperBody.mask";

        private const string IdleFbxPath = "Assets/Project/Models/ai/anim/Idle.fbx";
        private const string RifleDir = "Assets/Project/Models/ai/anim/rifle/";

        [MenuItem("WheatFarm/Reset Rifle Anim Root Transform Settings")]
        public static void ResetRifleAnimRootTransforms()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Project/Models/ai/anim/rifle" });
            int changed = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) continue;

                bool anyChanged = false;
                foreach (var clip in clips)
                {
                    // Revert bake flags to Unity defaults for Mixamo-style clips.
                    if (clip.lockRootRotation) { clip.lockRootRotation = false; anyChanged = true; }
                    if (!clip.keepOriginalOrientation) { clip.keepOriginalOrientation = true; anyChanged = true; }
                    if (Mathf.Abs(clip.rotationOffset) > 0.0001f) { clip.rotationOffset = 0f; anyChanged = true; }

                    if (clip.lockRootHeightY) { clip.lockRootHeightY = false; anyChanged = true; }
                    if (!clip.keepOriginalPositionY) { clip.keepOriginalPositionY = true; anyChanged = true; }
                    if (clip.heightFromFeet) { clip.heightFromFeet = false; anyChanged = true; }
                    if (Mathf.Abs(clip.heightOffset) > 0.0001f) { clip.heightOffset = 0f; anyChanged = true; }

                    if (clip.lockRootPositionXZ) { clip.lockRootPositionXZ = false; anyChanged = true; }
                    if (!clip.keepOriginalPositionXZ) { clip.keepOriginalPositionXZ = true; anyChanged = true; }

                    // Keep Loop Time ON (that's what actually makes walk cycles work).
                    if (!clip.loopTime) { clip.loopTime = true; anyChanged = true; }
                }

                if (anyChanged)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log($"[FarmerAnimatorSetup] Reset root transform settings on {changed} FBX (kept Loop Time ON).");
        }

        [MenuItem("WheatFarm/Enable Loop Time On Rifle Anims")]
        public static void EnableLoopTimeOnRifleAnims()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Project/Models/ai/anim/rifle" });
            int changed = 0;
            int skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { skipped++; continue; }

                var defaults = importer.defaultClipAnimations;
                if (defaults == null || defaults.Length == 0) { skipped++; continue; }

                bool anyChanged = false;
                foreach (var clip in defaults)
                {
                    if (!clip.loopTime)
                    {
                        clip.loopTime = true;
                        anyChanged = true;
                    }
                }

                if (anyChanged)
                {
                    importer.clipAnimations = defaults;
                    importer.SaveAndReimport();
                    changed++;
                }
                else
                {
                    skipped++;
                }
            }

            Debug.Log($"[FarmerAnimatorSetup] Loop Time pass: {changed} FBX updated, {skipped} skipped (already looped or no clips).");
        }

        [MenuItem("WheatFarm/Setup Farmer Locomotion BlendTree")]
        public static void SetupBlendTree()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[FarmerAnimatorSetup] Controller not found at {ControllerPath}");
                return;
            }

            var log = new StringBuilder();

            // 1. Ensure parameters exist.
            EnsureFloatParam(controller, "MoveX", log);
            EnsureFloatParam(controller, "MoveZ", log);
            EnsureFloatParam(controller, "Speed", log);

            // 2. Load clips.
            var idleClip = LoadAnimationClipFromFbx(IdleFbxPath);
            if (idleClip == null)
            {
                Debug.LogError($"[FarmerAnimatorSetup] Idle clip not found at {IdleFbxPath}");
                return;
            }

            var walk = LoadWalkClips(log);
            var run = LoadRunClips(log);

            // 3. Ensure UpperBody mask has correct humanoid body parts (NOT just transforms).
            var upperMask = EnsureUpperBodyMask(log);

            // 4. Rebuild Base Layer with Locomotion blend tree (no mask — full body).
            RebuildBaseLayer(controller, idleClip, walk, run, log);

            // 5. Ensure Upper Idle Layer exists (plays idleClip on upper body).
            EnsureUpperIdleLayer(controller, idleClip, upperMask, log);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FarmerAnimatorSetup] Done:\n" + log);
        }

        // ---------- parameters ----------

        private static void EnsureFloatParam(AnimatorController controller, string name, StringBuilder log)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = name,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0f,
            });
            log.AppendLine($"Added parameter {name}");
        }

        // ---------- clip loading ----------

        private class DirectionalClipSet
        {
            public AnimationClip Forward;
            public AnimationClip Backward;
            public AnimationClip Left;
            public AnimationClip Right;
            public AnimationClip ForwardLeft;
            public AnimationClip ForwardRight;
            public AnimationClip BackwardLeft;
            public AnimationClip BackwardRight;
        }

        private static DirectionalClipSet LoadWalkClips(StringBuilder log)
        {
            var set = new DirectionalClipSet
            {
                Forward = LoadAnimationClipFromFbx(RifleDir + "walk forward.fbx"),
                Backward = LoadAnimationClipFromFbx(RifleDir + "walk backward.fbx"),
                Left = LoadAnimationClipFromFbx(RifleDir + "walk left.fbx"),
                Right = LoadAnimationClipFromFbx(RifleDir + "walk right.fbx"),
                ForwardLeft = LoadAnimationClipFromFbx(RifleDir + "walk forward left.fbx"),
                ForwardRight = LoadAnimationClipFromFbx(RifleDir + "walk forward right.fbx"),
                BackwardLeft = LoadAnimationClipFromFbx(RifleDir + "walk backward left.fbx"),
                BackwardRight = LoadAnimationClipFromFbx(RifleDir + "walk backward right.fbx"),
            };
            log.AppendLine($"Walk clips: F={set.Forward?.name}, B={set.Backward?.name}, L={set.Left?.name}, R={set.Right?.name}, FL={set.ForwardLeft?.name}, FR={set.ForwardRight?.name}, BL={set.BackwardLeft?.name}, BR={set.BackwardRight?.name}");
            return set;
        }

        private static DirectionalClipSet LoadRunClips(StringBuilder log)
        {
            var set = new DirectionalClipSet
            {
                Forward = LoadAnimationClipFromFbx(RifleDir + "run forward.fbx"),
                Backward = LoadAnimationClipFromFbx(RifleDir + "run backward.fbx"),
                Left = LoadAnimationClipFromFbx(RifleDir + "run left.fbx"),
                Right = LoadAnimationClipFromFbx(RifleDir + "run right.fbx"),
                ForwardLeft = LoadAnimationClipFromFbx(RifleDir + "run forward left.fbx"),
                ForwardRight = LoadAnimationClipFromFbx(RifleDir + "run forward right.fbx"),
                BackwardLeft = LoadAnimationClipFromFbx(RifleDir + "run backward left.fbx"),
                BackwardRight = LoadAnimationClipFromFbx(RifleDir + "run backward right.fbx"),
            };
            log.AppendLine($"Run clips:  F={set.Forward?.name}, B={set.Backward?.name}, L={set.Left?.name}, R={set.Right?.name}, FL={set.ForwardLeft?.name}, FR={set.ForwardRight?.name}, BL={set.BackwardLeft?.name}, BR={set.BackwardRight?.name}");
            return set;
        }

        private static AnimationClip LoadAnimationClipFromFbx(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath)) return null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null || assets.Length == 0) return null;

            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        // ---------- avatar masks ----------

        /// <summary>
        /// Update the UpperBody avatar mask to enable only upper-body humanoid parts.
        /// The existing mask in the project was all-ones which effectively disables the mask.
        /// </summary>
        private static AvatarMask EnsureUpperBodyMask(StringBuilder log)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
                log.AppendLine("Created UpperBody.mask");
            }

            // Humanoid body parts: we want upper body (head, arms, fingers) active and
            // lower body (legs, feet IK) disabled. Body (spine/hips) is disabled so the
            // pelvis twist from rifle locomotion still drives the character.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);

            EditorUtility.SetDirty(mask);
            log.AppendLine("UpperBody mask configured: Head + Arms + Fingers + HandIK only");
            return mask;
        }

        // ---------- base layer ----------

        private static void RebuildBaseLayer(AnimatorController controller, AnimationClip idleClip,
            DirectionalClipSet walk, DirectionalClipSet run, StringBuilder log)
        {
            var layers = controller.layers;
            var baseLayer = layers[0];
            var sm = baseLayer.stateMachine;

            // IMPORTANT: Base Layer (index 0) must NOT have an avatar mask. Humanoid retargeting
            // handles character orientation/root motion on the default layer, and a mask breaks that.
            // We let the full rifle body animation play here; the upper body is then overwritten by
            // Upper Idle Layer (index 1) and Actions Layer (index 2), both of which use UpperBody mask.
            baseLayer.avatarMask = null;
            baseLayer.defaultWeight = 1f;

            // Remove all existing states from base layer (Idle, Walk, Locomotion, whatever).
            foreach (var cs in sm.states.ToList())
            {
                if (cs.state.motion is BlendTree oldTree)
                    Object.DestroyImmediate(oldTree, true);
                sm.RemoveState(cs.state);
            }

            // Create Locomotion state with nested blend tree.
            var locomotionState = sm.AddState("Locomotion", new Vector3(200, 0, 0));
            sm.defaultState = locomotionState;

            // Root: 1D blend by Speed between WalkTree and RunTree (with Idle at Speed=0).
            var root = new BlendTree
            {
                name = "LocomotionRoot",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(root, controller);

            // Walk tree (9-direction 2D freeform).
            var walkTree = BuildDirectionalTree(controller, "WalkTree", idleClip, walk);
            // Run tree.
            var runTree = BuildDirectionalTree(controller, "RunTree", idleClip, run);

            // 1D children: 0=idle, 0.5=walk, 1.0=run.
            // Idle child uses an inline idleClip via a simple "idle" sub-tree (just the clip).
            var idleTree = new BlendTree
            {
                name = "IdleTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = true,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(idleTree, controller);
            idleTree.AddChild(idleClip, 0f);

            root.AddChild(idleTree, 0f);
            root.AddChild(walkTree, 0.5f);
            root.AddChild(runTree, 1f);

            locomotionState.motion = root;

            // Ensure layer array is updated (AnimatorController.layers returns a copy).
            layers[0] = baseLayer;
            controller.layers = layers;

            log.AppendLine("Base Layer rebuilt: Locomotion = 1D(Speed) [Idle | WalkTree(2D) | RunTree(2D)]");
        }

        private static BlendTree BuildDirectionalTree(AnimatorController controller, string name,
            AnimationClip fallback, DirectionalClipSet set)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            // 9-point directional layout. Freeform Directional requires at least one child
            // at the center (0,0). All 8 cardinal + diagonal points plus center.
            // Using fallback (idle) when a clip is missing avoids errors.
            AnimationClip C(AnimationClip clip) => clip != null ? clip : fallback;

            // center (idle blend) to avoid empty-center warnings:
            tree.AddChild(fallback, new Vector2(0f, 0f));

            // 8 directions (Unity's convention: X=right, Z=forward). Normalize diagonals so their
            // magnitudes are similar to cardinal axes — BlendTree handles interpolation by angle.
            const float d = 0.7071f; // sqrt(2)/2 for unit diagonals

            tree.AddChild(C(set.Forward),       new Vector2(0f,  1f));
            tree.AddChild(C(set.Backward),      new Vector2(0f, -1f));
            tree.AddChild(C(set.Left),          new Vector2(-1f, 0f));
            tree.AddChild(C(set.Right),         new Vector2(1f,  0f));
            tree.AddChild(C(set.ForwardLeft),   new Vector2(-d,  d));
            tree.AddChild(C(set.ForwardRight),  new Vector2(d,   d));
            tree.AddChild(C(set.BackwardLeft),  new Vector2(-d, -d));
            tree.AddChild(C(set.BackwardRight), new Vector2(d,  -d));

            return tree;
        }

        // ---------- upper idle layer ----------

        private static void EnsureUpperIdleLayer(AnimatorController controller, AnimationClip idleClip,
            AvatarMask upperMask, StringBuilder log)
        {
            var layers = controller.layers.ToList();

            // Remove existing Upper Idle layer if any (so we rebuild cleanly).
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].name == "Upper Idle")
                {
                    // Clean up sub-assets of that layer's state machine.
                    var oldSm = layers[i].stateMachine;
                    if (oldSm != null)
                    {
                        foreach (var cs in oldSm.states.ToList())
                        {
                            if (cs.state.motion is BlendTree bt) Object.DestroyImmediate(bt, true);
                            Object.DestroyImmediate(cs.state, true);
                        }
                        Object.DestroyImmediate(oldSm, true);
                    }
                    layers.RemoveAt(i);
                }
            }

            // Build a new state machine for Upper Idle.
            var upperSm = new AnimatorStateMachine
            {
                name = "Upper Idle",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(upperSm, controller);

            var state = upperSm.AddState("UpperIdle");
            state.motion = idleClip;
            upperSm.defaultState = state;

            var upperIdleLayer = new AnimatorControllerLayer
            {
                name = "Upper Idle",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = upperMask,
                stateMachine = upperSm,
                iKPass = false,
            };

            // Insert after Base Layer (index 1), so Actions Layer (upper mask) remains last and overrides.
            layers.Insert(1, upperIdleLayer);
            controller.layers = layers.ToArray();

            log.AppendLine("Added 'Upper Idle' layer with UpperBody mask playing Idle clip on upper body.");
        }
    }
}
#endif
