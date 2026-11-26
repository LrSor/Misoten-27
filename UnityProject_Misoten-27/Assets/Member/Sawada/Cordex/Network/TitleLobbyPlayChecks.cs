#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sawada.Cordex
{
    // Explicit opt-in, development builds only. Exercises the real title button and transports.
    public sealed class TitleLobbyPlayChecks : MonoBehaviour
    {
        TitleLobbyFlow flow;
        float started;
        bool passed;
        static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RunIfRequested()
        {
            if (Arg("--lobby-check-mode").Length > 0)
                new GameObject("Title Lobby Play Checks").AddComponent<TitleLobbyPlayChecks>();
        }

        T Read<T>(string name) => (T)typeof(TitleLobbyFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);
        void Write(string name, object value) => typeof(TitleLobbyFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, value);
        void Call(string name) => typeof(TitleLobbyFlow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        void Click(string name)
        {
            var button = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
            Require(button.interactable, name + " is disabled");
            button.onClick.Invoke();
        }

        void CheckModels()
        {
            var canvas = SceneManager.GetActiveScene().GetRootGameObjects().Single(r => r.name == "Canvas");
            var members = Read<int[]>("members");
            for (int id = 1; id <= 4; id++)
                Require(canvas.transform.Find("Player" + id + "/Model").gameObject.activeSelf == members.Contains(id), "Model differs from roster P" + id);
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            started = Time.realtimeSinceStartup;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            yield return null;
            flow = FindFirstObjectByType<TitleLobbyFlow>();
            Require(flow != null, "Flow missing from Title");
            var button = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(b => b.name == "ButtonNewGame");
            button.onClick.Invoke();
            Require(Read<bool>("menu") && !Read<bool>("internet"), "Start must open the LAN menu");
            if (Arg("--lobby-check-mode") == "preview")
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Arg("--lobby-check-capture"));
                yield return new WaitForSecondsRealtime(1f);
                Pass(); yield break;
            }
            Write("portInput", Arg("--lobby-check-port", "17967"));
            string mode = Arg("--lobby-check-mode");
            if (mode == "host" || mode == "host-exit") Call("CreateRoom");
            else
            {
                Write("input", "invalid-code"); Call("JoinRoom");
                Require(!Read<bool>("busy"), "Invalid code attempted a connection");
                Write("input", Arg("--lobby-check-code")); Call("JoinRoom");
            }
            if (mode == "reject" || mode == "missing")
            {
                while (Read<bool>("busy")) yield return null;
                string expected = mode == "reject" ? "Room full" : "failed";
                Require(Read<string>("status").Contains(expected), "Expected failure: " + expected);
                Pass(); yield break;
            }
            while (SceneManager.GetActiveScene().name != TitleLobbyFlow.LobbyScene || !Read<bool>("inRoom")) yield return null;
            Require(Read<bool>("busy"), "Lobby must retain its connection");
            yield return null;
            Require(PlayerLobbyView.Active != null && PlayerLobbyView.Active.HasValidReferences, "Lobby UI missing");
            CheckModels();
            GUIUtility.systemCopyBuffer = "clipboard-check";
            Click("CopyButton");
            Require(GUIUtility.systemCopyBuffer == Read<string>("code"), "Copy button did not copy the room number");
            Debug.Log("[TitleLobbyCheck] WAITING " + Read<string>("code"));
            string capture = Arg("--lobby-check-capture");
            if (capture.Length > 0) ScreenCapture.CaptureScreenshot(capture);
            if (mode == "leave")
            {
                float leaveAfter = float.Parse(Arg("--lobby-check-leave-delay", "0"));
                if (leaveAfter > 0) yield return new WaitForSecondsRealtime(leaveAfter);
                Click("ExitButton");
                while (SceneManager.GetActiveScene().name != TitleLobbyFlow.TitleScene) yield return null;
                Require(!Read<bool>("busy"), "Client stayed connected after exit");
                Debug.Log("[TitleLobbyCheck] LEFT");
                Pass(); yield break;
            }
            if (mode == "host-exit" || mode == "closed")
            {
                int count = int.Parse(Arg("--lobby-check-players", "3"));
                while (Read<int[]>("members").Length < count) yield return null;
                CheckModels();
                if (mode == "host-exit") { yield return new WaitForSecondsRealtime(2); Click("ExitButton"); }
                while (SceneManager.GetActiveScene().name != TitleLobbyFlow.TitleScene) yield return null;
                Require(!Read<bool>("busy"), "Host closure did not disconnect this player");
                Debug.Log("[TitleLobbyCheck] HOST CLOSED -> TITLE");
                Pass(); yield break;
            }
            string expectedNumber = Arg("--lobby-check-final-number");
            if (expectedNumber.Length > 0)
            {
                while (Read<int>("localId") != int.Parse(expectedNumber)) yield return null;
                yield return null;
                CheckModels();
                Debug.Log("[TitleLobbyCheck] COMPACTED P" + Read<int>("localId"));
            }
            int targetCount = int.Parse(Arg("--lobby-check-players", "3"));
            while (Read<int[]>("members").Length != targetCount) yield return null;
            string readyFile = Arg("--lobby-check-ready-file");
            while (readyFile.Length > 0 && !File.Exists(readyFile)) yield return null;
            if (mode == "host")
            {
                // Leave time for a fourth Client to attempt to join a full room.
                float until = Time.realtimeSinceStartup + float.Parse(Arg("--lobby-check-start-delay", "4"));
                while (Time.realtimeSinceStartup < until) yield return null;
            }
            float readyDelay = float.Parse(Arg("--lobby-check-ready-delay", "0"));
            if (readyDelay > 0)
            {
                yield return new WaitForSecondsRealtime(readyDelay);
                Require(!Read<bool>("loading"), "Game started before this player was prepared");
            }
            CheckModels();
            if (mode == "host")
            {
                Click("ReadyButton");
                Require(flow.IsPlayerPrepared(1), "Host Ready did not update");
                Click("ReadyButton");
                Require(!flow.IsPlayerPrepared(1), "Host Ready cancel did not update");
                Debug.Log("[TitleLobbyCheck] READY CANCEL OK");
            }
            Click("ReadyButton");
            Debug.Log("[TitleLobbyCheck] READY P" + Read<int>("localId"));
            float loadDelay = float.Parse(Arg("--lobby-check-load-delay", "0"));
            if (loadDelay > 0)
            {
                while (!Read<bool>("loading")) yield return null;
                var operation = Read<AsyncOperation>("sceneLoad");
                operation.allowSceneActivation = false;
                Debug.Log("[TitleLobbyCheck] DELAYING GAME LOAD");
                yield return new WaitForSecondsRealtime(loadDelay);
                Require(!Read<bool>("handedOff"), "Game started before the slow Client was ready");
                operation.allowSceneActivation = true;
            }
            while (!Read<bool>("handedOff")) yield return null;
            Require(SceneManager.GetActiveScene().name == TitleLobbyFlow.GameScene, "Wrong game scene");
            var session = FindFirstObjectByType<NetworkTestSession>();
            float settle = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Require(session != null && session.SuzukiIsBusy && session.SuzukiLocalId > 0, "Gameplay connection missing");
            Require(session.SuzukiPlayerCount == int.Parse(Arg("--lobby-check-players", "3")), "Gameplay roster differs from lobby");
            Require(GameObject.Find("Network Player " + session.SuzukiLocalId) != null, "Owned player missing");
            Debug.Log("[TitleLobbyCheck] GAMEPLAY P" + session.SuzukiLocalId + " players=" + session.SuzukiPlayerCount);
            if (Arg("--lobby-check-results") == "yes")
            {
                int originalId = flow.PlayerNumber;
                string originalCode = flow.RoomCode;
                int[] scores = { 100, 200, 200, 0 };
                int[] expectedRanks = { 3, 1, 1, 4 };
                session.LocalMiniGamePlayerScore.SetMiniGameScore(null, scores[originalId - 1]);
                while (SceneManager.GetActiveScene().name != TitleLobbyFlow.ResultScene) yield return null;
                yield return null;
                Require(flow.ResultEntries.Count == 4, "Final result is missing participants");
                var resultCanvas = SceneManager.GetActiveScene().GetRootGameObjects().Single(r => r.name == "Canvas");
                for (int id = 1; id <= 4; id++)
                {
                    var entry = flow.ResultEntries.Single(e => e.id == id);
                    Require(entry.score == scores[id - 1] && entry.rank == expectedRanks[id - 1] && entry.submitted,
                        "Final rank / score differs for P" + id);
                    Require(resultCanvas.transform.Find("Player" + id + "/Rank").GetComponent<TMPro.TMP_Text>().text == expectedRanks[id - 1] + "位",
                        "Rank UI differs for P" + id);
                    Require(resultCanvas.transform.Find("Player" + id + "/Model").gameObject.activeSelf, "Result Model hidden");
                }
                Require(Time.timeScale > 0f, "Result scene remained paused");
                Debug.Log("[TitleLobbyCheck] RESULT RANKS OK P" + originalId);
                yield return new WaitForSecondsRealtime(float.Parse(Arg("--lobby-check-result-exit-delay", "1")));
                Click("ExitButton");
                while (SceneManager.GetActiveScene().name != TitleLobbyFlow.LobbyScene) yield return null;
                yield return null;
                Require(flow.IsInRoom && flow.PlayerNumber == originalId && flow.RoomCode == originalCode, "Result exit lost the room");
                Debug.Log("[TitleLobbyCheck] RESULT -> SAME LOBBY P" + originalId);
                Click("ReadyButton");
                while (SceneManager.GetActiveScene().name != TitleLobbyFlow.GameScene || !Read<bool>("handedOff")) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                session = FindFirstObjectByType<NetworkTestSession>();
                Require(session != null && session.SuzukiLocalId == originalId && session.SuzukiPlayerCount == 4, "Rematch lost participants");
                Require(GameTimeManager.Instance != null && !GameTimeManager.Instance.HasEnded && GameTimeManager.Instance.GameTimer > 0f,
                    "Rematch timer did not reset");
                Require(session.LocalMiniGamePlayerScore.GetTotalScore() == 0, "Rematch score was not reset");
                Debug.Log("[TitleLobbyCheck] REMATCH OK P" + originalId);
            }
            // Keep peers alive until the common verification deadline.
            passed = true;
        }

        void Pass() { passed = true; Debug.Log("[TitleLobbyCheck] PASS"); Application.Quit(0); }

        void Update()
        {
            if (started <= 0) return;
            if (Time.realtimeSinceStartup - started < float.Parse(Arg("--lobby-check-duration", "45"))) return;
            Debug.Log(passed ? "[TitleLobbyCheck] PASS" : "[TitleLobbyCheck] FAIL timeout");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
