using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using SneakHouse;

namespace SneakHouseEditor
{
    public static class GuardEncounterBuilder
    {
        private const string Root = "Assets/SneakHouse/";
        [MenuItem("Sneak House/Set up randomized guard encounters")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            ThreeGuardBuilder.Apply();
            GameSession game = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            foreach (string folder in new[] { "Prefabs/Guards", "Animations" })
                if (!AssetDatabase.IsValidFolder(Root + folder))
                    AssetDatabase.CreateFolder(Root + (folder.Contains("/") ? folder.Substring(0,folder.LastIndexOf('/')) : "").TrimEnd('/'),folder.Split('/').Last());
            GuardEncounter.Character[] roster =
            {
                Character("bogo","Bogo","Actors/01_bogo_guard",1.8f),
                Character("judy","Judy","PortraitCast/03_judy_guard",1.65f),
                Character("clawhauser","Clawhauser","Actors/02_clawhauser_guard",1.8f),
                Character("spike","Spike","Actors/04_spike_guard",1.55f)
            };
            GuardEncounter encounter = game.GetComponent<GuardEncounter>();
            if (encounter == null) encounter = Undo.AddComponent<GuardEncounter>(game.gameObject);
            Undo.RecordObject(encounter,"Configure guard roster");
            encounter.roster = roster;
            encounter.zones = game.guards.Select(g => new GuardEncounter.Zone { guard = g, route = g.patrol.ToArray() }).ToArray();
            EditorUtility.SetDirty(encounter);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        private static GuardEncounter.Character Character(string id, string label, string model, float height)
        {
            string modelPath = Root + "Models/" + model + ".fbx", file = model.Split('/').Last();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidOperationException("Missing actor model: " + modelPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            foreach (string state in new[] { "Idle", "Walk", "Run", "LookAround", "FaceActing" })
                if (!clips.Any(c => c.name == state)) throw new InvalidOperationException(label + " is missing clip " + state);
            GameObject holder = new GameObject("Guard visual calibration / temporary");
            try
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source,holder.transform);
                visual.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
                Bounds bounds = BoundsOf(visual);
                float scale = Mathf.Min(height / bounds.size.y, 1.6f / Mathf.Max(bounds.size.x,bounds.size.z));
                visual.transform.localScale *= scale;
                visual.transform.position -= Vector3.up * BoundsOf(visual).min.y;
                Transform[] bones = visual.GetComponentsInChildren<Transform>();
                Transform eye = bones.First(t => t.name.StartsWith("face_eye")), head = bones.First(t => t.name == "head");
                Vector3 forward = Vector3.ProjectOnPlane(eye.position - head.position,Vector3.up);
                if (forward.sqrMagnitude < .0001f) throw new InvalidOperationException("Cannot determine facing for " + label);
                visual.transform.Rotate(0,Vector3.SignedAngle(forward,Vector3.forward,Vector3.up),0,Space.World);
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ActorAnimation acting = holder.AddComponent<ActorAnimation>(); acting.animator = animator;
                string controllerPath = Root + "Prefabs/Guards/" + id + ".controller";
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                foreach (ChildAnimatorState state in machine.states) machine.RemoveState(state.state);
                foreach (string state in new[] { "Idle", "Walk", "Run", "LookAround", "FaceActing" })
                {
                    AnimationClip clip = clips.First(c => c.name == state);
                    if (state == "Walk" || state == "Run")
                    {
                        string path = Root + "Animations/" + file + "_" + state + ".anim";
                        AnimationClip calibrated = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (calibrated == null || id != "bogo")
                        {
                            float error;
                            AnimationClip baked = LocomotionBuilder.Bake(acting,clip,state == "Walk" ? .65f : .44f,
                                state == "Walk" ? 1.4f : 3.2f,state == "Walk" ? .56f : .36f,out error);
                            if (error > .03f) { UnityEngine.Object.DestroyImmediate(baked); throw new InvalidOperationException(label + " locomotion IK error " + error); }
                            if (calibrated == null) { calibrated = baked; AssetDatabase.CreateAsset(calibrated,path); }
                            else { EditorUtility.CopySerialized(baked,calibrated); UnityEngine.Object.DestroyImmediate(baked); }
                            calibrated.name = state; EditorUtility.SetDirty(calibrated);
                        }
                        clip = calibrated;
                    }
                    AnimatorState node = machine.AddState(state); node.motion = clip;
                    if (state == "Idle") machine.defaultState = node;
                }
                animator.runtimeAnimatorController = controller; EditorUtility.SetDirty(controller);
                foreach (Transform bone in bones) bone.gameObject.layer = 9;
                foreach (SkinnedMeshRenderer renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
                // Baker samples a clone, so the authored visual remains in its rest pose.
                float eyeHeight = eye.position.y;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(visual,Root + "Prefabs/Guards/" + id + ".prefab");
                return new GuardEncounter.Character { id = id, displayName = label, visualPrefab = prefab,
                    eyeHeight = eyeHeight, bodyHeight = Mathf.Max(1.3f,BoundsOf(visual).size.y) };
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
        private static Bounds BoundsOf(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds result = renderers[0].bounds;
            foreach (Renderer renderer in renderers) result.Encapsulate(renderer.bounds);
            return result;
        }
    }
}
