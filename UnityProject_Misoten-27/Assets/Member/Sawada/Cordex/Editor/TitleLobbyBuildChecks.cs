using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Sawada.Cordex.Editor
{
    // Fixed-purpose command entry point for an already open Editor.
    // Write a GUID to Logs/TitleLobby-build.request; read the matching JSON result.
    [InitializeOnLoad]
    internal static class TitleLobbyBuildCommand
    {
        [Serializable] sealed class Reply
        {
            public string requestId, error;
            public bool success;
        }

        static double nextPoll;
        static TitleLobbyBuildCommand() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1d;
            const string requestPath = "Logs/TitleLobby-build.request";
            const string resultPath = "Logs/TitleLobby-build.result.json";
            if (!File.Exists(requestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            string requestId = File.ReadAllText(requestPath).Trim();
            File.Delete(requestPath);
            var reply = new Reply { requestId = requestId };
            try
            {
                if (!Guid.TryParseExact(requestId, "N", out _)) throw new Exception("Invalid build request ID");
                Debug.Log("[TitleLobbyBuildCommand] BEGIN " + requestId);
                TitleLobbyBuildChecks.Build();
                reply.success = true;
            }
            catch (Exception e)
            {
                reply.error = e.Message;
                Debug.LogException(e);
            }
            File.WriteAllText(resultPath, JsonUtility.ToJson(reply, true));
            Debug.Log("[TitleLobbyBuildCommand] END " + requestId + " success=" + reply.success);
        }
    }

    public static class TitleLobbyBuildChecks
    {
        [MenuItem("Sawada/Cordex/Build Title Lobby")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before building");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new Exception("Save scene edits before building the lobby");
            string[] scenes = { "Assets/Main_KAri/Scene/Title_Kari.unity", "Assets/Main_KAri/Scene/PlayerLobby.unity",
                "Assets/Main_KAri/Scene/Game_11_26_02.unity", "Assets/Main_KAri/Scene/Result_Kari.unity" };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            // Preserve known Unity build caches, including assets outside our edit scope.
            string[] caches = { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/Member/Sawada/Font/keifont SDF.asset", "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/UnityConnectSettings.asset", "Assets/Resources/PerformanceTestRunInfo.json",
                "Assets/Resources/PerformanceTestRunInfo.json.meta", "Assets/Resources/PerformanceTestRunSettings.json",
                "Assets/Resources/PerformanceTestRunSettings.json.meta" };
            var before = caches.Where(File.Exists).ToDictionary(p => p, File.ReadAllBytes);
            try
            {
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    int missing = roots.Sum(r => r.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                    if (missing != 0) throw new Exception(path + ": Missing Scripts = " + missing);
                    if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path))
                        throw new Exception("Scene not registered: " + path);
                    if (path.Contains("Title_Kari"))
                    {
                        var eventSystem = roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>())
                            .SingleOrDefault(e => e.isActiveAndEnabled);
                        if (eventSystem == null || !eventSystem.GetComponents<BaseInputModule>().Any(m => m.isActiveAndEnabled))
                            throw new Exception("Title UI requires an active EventSystem and input module");
                        var start = roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)).Single(b => b.name == "ButtonNewGame");
                        if (!(start.onClick.GetPersistentTarget(0) is TitleLobbyFlow) || start.onClick.GetPersistentMethodName(0) != "OpenMenu")
                            throw new Exception("Title Start button is not wired to the lobby");
                    }
                    if (path.Contains("Game_11_26_02"))
                    {
                        var session = roots.SelectMany(r => r.GetComponentsInChildren<NetworkTestSession>(true)).Single();
                        var prefab = new SerializedObject(session).FindProperty("playerPrefab").objectReferenceValue as GameObject;
                        if (prefab == null || prefab.GetComponentsInChildren<Transform>(true)
                            .Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                            throw new Exception("Player prefab / scripts invalid");
                    }
                    if (path.Contains("PlayerLobby"))
                    {
                        var view = roots.SelectMany(r => r.GetComponentsInChildren<PlayerLobbyView>(true)).Single();
                        if (!view.HasValidReferences) throw new Exception("Lobby controls or Model references are missing");
                    }
                    if (path.Contains("Result_Kari"))
                    {
                        var view = roots.SelectMany(r => r.GetComponentsInChildren<MatchResultView>(true)).Single();
                        if (!view.HasValidReferences) throw new Exception("Result Rank / Model / Exit references are missing");
                    }
                    Debug.Log("[TitleLobbyCheck] SCENE OK " + path);
                }
                Directory.CreateDirectory("Builds/TitleLobby");
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes,
                    locationPathName = "Builds/TitleLobby/TitleLobby.exe", target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development });
                if (result.summary.result != BuildResult.Succeeded) throw new Exception("Lobby build failed: " + result.summary.result);
                Debug.Log("[TitleLobbyCheck] BUILD SUCCEEDED");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                foreach (var item in before)
                    if (!File.Exists(item.Key) || !File.ReadAllBytes(item.Key).SequenceEqual(item.Value)) File.WriteAllBytes(item.Key, item.Value);
                ReloadRestoredFont();
            }
        }

        internal static void ReloadRestoredFont()
        {
            // TMP clears dynamic glyph data during builds. Restoring the file alone
            // leaves its loaded lookup tables / atlas out of sync in the Editor.
            const string path = "Assets/Member/Sawada/Font/keifont SDF.asset";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) throw new Exception("Restored lobby font could not be loaded");
            font.ReadFontAssetDefinition();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);
            SceneView.RepaintAll();
        }
    }
}
