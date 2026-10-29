using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sawada.Cordex.Editor
{
    public static class NetworkTestBuild
    {
        public const string ScenePath = "Assets/Member/Sawada/Scenes/testMapPlayerMove.unity";
        [MenuItem("Sawada/Cordex/Configure Current Map and Build Network Test")]
        public static void ConfigureCurrentMapAndBuild()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new Exception("Open " + ScenePath + " before configuring");
            if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before configuring");
            // Work on the open scene to preserve any existing unsaved user edits.
            var session = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NetworkTestSession>(true)).FirstOrDefault();
            if (session == null) session = new GameObject("NetworkTestSession").AddComponent<NetworkTestSession>();
            session.gameObject.SetActive(true);
            var serialized = new SerializedObject(session);
            serialized.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Member/Sawada/Prefab/Player/Player_ver2.prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var root in scene.GetRootGameObjects())
                if (root != session.gameObject && root.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null)
                    root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save network map");
            Build();
        }
        [MenuItem("Sawada/Cordex/Build Windows Network Test")]
        public static void Build()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new Exception("Save your scene edits before building the network test");
            // Unity/URP can serialize build caches into source settings. Preserve their exact bytes.
            string[] generatedSettings = {
                "Assets/Settings/PC_RPAsset.asset",
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/Member/Sawada/Font/keifont SDF.asset",
                "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/UnityConnectSettings.asset"
            };
            var settingsBefore = generatedSettings.Where(File.Exists).ToDictionary(p => p, File.ReadAllBytes);
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                int missing = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true)
                    .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                if (missing != 0) throw new Exception("Target scene contains missing scripts: " + missing);
                var session = UnityEngine.Object.FindFirstObjectByType<NetworkTestSession>();
                if (session == null) throw new Exception("NetworkTestSession is missing");
                var prefab = new SerializedObject(session).FindProperty("playerPrefab").objectReferenceValue as GameObject;
                if (prefab == null || prefab.GetComponentsInChildren<Transform>(true)
                    .Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                    throw new Exception("Player prefab reference or scripts invalid");
                string path = Path.GetFullPath("Builds/NetworkTest/NetworkTest.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                Directory.CreateDirectory("Builds/NetworkTest/Verification");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                File.WriteAllText("Builds/NetworkTest/Verification/build-result.txt",
                    report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\n" + path);
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
                Debug.Log("[NetworkTest] BUILD SUCCEEDED " + path);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
                foreach (var entry in settingsBefore)
                    if (!File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)) File.WriteAllBytes(entry.Key, entry.Value);
            }
        }
    }
}
