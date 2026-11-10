using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sawada.Cordex.Editor
{
    public static class EntitiesTrialChecks
    {
        [MenuItem("Sawada/Cordex/Validate and Build Entities Trial")]
        public static void ValidateAndBuild()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new Exception("Save scene changes before validating");
            try
            {
                foreach (string path in new[] { "Assets/Member/Sawada/Scenes/CordexScene.unity", NetworkTestBuild.ScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int missing = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                    if (missing != 0) throw new Exception(path + " has Missing Scripts: " + missing);
                    var sessions = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NetworkTestSession>(true)).ToArray();
                    if (sessions.Length != 1) throw new Exception(path + " must have one network session");
                    var session = new SerializedObject(sessions[0]);
                    if (!session.FindProperty("useEntitiesTrial").boolValue) throw new Exception(path + " does not select Entities trial");
                    var prefab = session.FindProperty("playerPrefab").objectReferenceValue as GameObject;
                    if (prefab == null || prefab.GetComponentInChildren<Rigidbody>(true) == null ||
                        prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                        throw new Exception(path + " has invalid player prefab");
                }
                CheckMotor();
                CheckStaticCollision();
                CheckRenderPerformance();
                NetworkTestBuild.Build();
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/EntitiesTrial-validation.txt", "PASS: two scene references, Missing Script checks, replayable motor, Windows development build\n");
                Debug.Log("[EntitiesTrialChecks] PASS");
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static void CheckMotor()
        {
            var first = new TrialMotion { Position = new Vector3(10000, 10000, 10000) };
            var second = first;
            var command = new TrialInput { Move = new Unity.Mathematics.float2(1, 0), Forward = new Unity.Mathematics.float2(1, 0), SprintPress = 1 };
            for (int i = 0; i < 200; i++) TrialMotor.Step(ref first, command, .02f);
            for (int i = 0; i < 100; i++) TrialMotor.Step(ref second, command, .02f);
            var checkpoint = second;
            for (int i = 0; i < 100; i++) TrialMotor.Step(ref second, command, .02f);
            if (Unity.Mathematics.math.distance(first.Position, second.Position) > .0001f || first.SprintTime != second.SprintTime)
                throw new Exception("Motor replay diverged");
            if (!checkpoint.Sprinting || checkpoint.SprintTime < 1.99f || checkpoint.SprintTime > 2.01f)
                throw new Exception("Sprint state is not restorable");
            for (int i = 0; i < 100; i++) TrialMotor.Step(ref second, command, .02f);
            if (second.Sprinting) throw new Exception("Sprint did not finish");
        }

        static void CheckRenderPerformance()
        {
            var originalQuality = QualitySettings.renderPipeline;
            var original = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            using var performance = new NetworkRenderPerformance(new[] { "--net-render-profile", "standard" });
            if (!performance.CanConfigure)
            {
                Debug.Log("[EntitiesTrialChecks] Rendering profile check skipped: graphics device or URP unavailable");
                return;
            }
            float scale = original.renderScale, distance = original.shadowDistance;
            int cascades = original.shadowCascadeCount;
            var cameraObject = new GameObject("Runtime render profile validation camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                performance.RegisterCamera(camera);
                performance.SetLightweight(true);
                var lightweight = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (lightweight == null || lightweight == original || lightweight.renderScale > .8f ||
                    lightweight.shadowDistance > 20f || lightweight.shadowCascadeCount != 1 || data.renderPostProcessing)
                    throw new Exception("Runtime lightweight profile did not apply");
                if (original.renderScale != scale || original.shadowDistance != distance || original.shadowCascadeCount != cascades)
                    throw new Exception("Runtime profile changed the shared URP asset");
                performance.SetLightweight(false);
                if (QualitySettings.renderPipeline != originalQuality || !data.renderPostProcessing)
                    throw new Exception("Standard render settings were not restored");
                performance.SetLightweight(true);
                performance.Dispose();
                if (QualitySettings.renderPipeline != originalQuality || !data.renderPostProcessing)
                    throw new Exception("Render settings were not restored on dispose");
                Debug.Log("[EntitiesTrialChecks] Runtime render profile isolation and restoration PASS");
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
        }

        static void CheckStaticCollision()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                var floor = new GameObject("Trial check floor");
                floor.transform.position = new Vector3(10000, 0, 10000);
                floor.AddComponent<BoxCollider>().size = new Vector3(20, .2f, 20);
                var wall = new GameObject("Trial check wall");
                wall.transform.position = new Vector3(10004, 2, 10000);
                wall.AddComponent<BoxCollider>().size = new Vector3(.2f, 5, 20);
                Physics.SyncTransforms();
                var state = new TrialMotion { Position = new Vector3(10000, 1.2f, 10000) };
                var command = new TrialInput { Move = new Unity.Mathematics.float2(1, 0) };
                for (int i = 0; i < 200; i++) TrialMotor.Step(ref state, command, .02f);
                if (state.Position.x > 10003.43f || state.Position.x < 10003.3f || state.Position.y < 1.05f || state.Position.y > 1.15f)
                    throw new Exception("Static wall/floor collision failed: " + state.Position);
                command.Move = default;
                for (int i = 0; i < 30; i++) TrialMotor.Step(ref state, command, .02f);
                if (Unity.Mathematics.math.length(state.Velocity.xz) > .001f)
                    throw new Exception("Release input did not stop the motor");
            }
            finally { EditorSceneManager.CloseScene(scene, true); Physics.SyncTransforms(); }
        }
    }
}
