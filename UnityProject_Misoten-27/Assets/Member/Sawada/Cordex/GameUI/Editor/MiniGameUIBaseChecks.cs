using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>UI の遷移・操作復帰・Scene 参照を、Scene を保存せずに確認する。</summary>
public static class MiniGameUIBaseChecks
{
    [MenuItem("Tools/Sawada/Check MiniGame UI Base")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run MiniGame UI checks in Edit Mode.");
        GameObject owner = new GameObject("MiniGame UI check");
        GameObject player = new GameObject("Player control check");
        try
        {
            MiniGameUIBase ui = owner.AddComponent<MiniGameUIBase>();
            StateMachine machine = owner.GetComponent<StateMachine>();
            Camera control = player.AddComponent<Camera>();
            Light disabledControl = player.AddComponent<Light>();
            disabledControl.enabled = false;
            GameObject root = new GameObject("UI root");
            root.transform.SetParent(owner.transform);
            SetField(ui, "m_uiRoot", root);
            SetField(ui, "m_playerControls", new Behaviour[] { control, disabledControl, control, machine });
            SetField(machine, "m_initialState", nameof(MiniGameUIClosedState));
            Invoke(ui, "Awake");
            Invoke(machine, "Awake");
            Invoke(machine, "Start");
            Require(!ui.IsRunning && !root.activeSelf, "Initially closed");
            var playingState = owner.GetComponent<MiniGameUIPlayingState>();
            ui.ReceiveVector2Input(Vector2.one);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == Vector2.zero && !playingState.IsButtonPressed, "Closed ignores input");

            ui.BeginMiniGame();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation && !ui.CanControlPlayer,
                "Explanation locks game controls");
            Require(!control.enabled && !disabledControl.enabled && machine.enabled && root.activeSelf,
                "Only configured player controls are suspended");
            Require(ui.RemainingSeconds > 4.9f && ui.RemainingSeconds <= 5f, "Default explanation is five seconds");
            ui.BeginMiniGame();
            ui.FinishMiniGame();
            ui.ReceiveVector2Input(Vector2.one);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == Vector2.zero && !playingState.IsButtonPressed, "Explanation ignores input");
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation, "Repeated start and early finish are ignored");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 4f);
            owner.GetComponent<MiniGameUIExplanationState>().UpdateState();
            Require(!ui.CanControlPlayer, "Explanation stays locked before five seconds");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 5f);
            owner.GetComponent<MiniGameUIExplanationState>().UpdateState();
            Require(ui.CanControlPlayer && control.enabled && !disabledControl.enabled, "Play restores previous controls");
            // 値を丸めずに渡すため、マウス delta など 1 を超える入力も受け取れる。
            Vector2 sample = new Vector2(12f, -3f);
            ui.ReceiveVector2Input(sample);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == sample && playingState.IsButtonPressed, "Function input reaches playing State");
            ui.ReceiveButtonInput(false);
            Require(!playingState.IsButtonPressed, "Button release reaches playing State");
            ui.ReceiveButtonInput(true);

            ui.FinishMiniGame();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Result && !control.enabled, "Result locks controls");
            ui.ReceiveVector2Input(sample);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == Vector2.zero && !playingState.IsButtonPressed, "Exit resets input and result ignores it");
            Require(ui.RemainingSeconds > 4.9f, "Default result is five seconds");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 4f);
            owner.GetComponent<MiniGameUIResultState>().UpdateState();
            Require(ui.IsRunning, "Result remains open before five seconds");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 5f);
            owner.GetComponent<MiniGameUIResultState>().UpdateState();
            Require(!ui.IsRunning && !root.activeSelf && control.enabled && !disabledControl.enabled,
                "Result closes and restores original controls");

            ui.BeginMiniGame();
            Invoke(ui, "OnDisable");
            Require(!ui.IsRunning && control.enabled, "Interruption restores controls");
            ui.BeginMiniGame();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation, "Can restart after interruption");
            ui.CancelMiniGame();
            CheckScene();
            Debug.Log("MiniGameUIBaseChecks: PASS (timing, transitions, control restoration, restart, scene references)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    private static void CheckScene()
    {
        const string path = "Assets/Member/Sawada/Scenes/GameUI.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool openedForCheck = !scene.isLoaded;
        if (openedForCheck) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            TestUIController testUI = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                        "No Missing Script: " + child.name);
                    if (child.TryGetComponent(out TestUIController found)) testUI = found;
                }
            Require(testUI != null, "Scene contains TestUIController");
            Require(testUI.GetComponents<BaseState>().Length == 4, "Scene contains all four UI states");
            StateMachine machine = testUI.GetComponent<StateMachine>();
            Require(machine != null, "Scene contains StateMachine");
            Require((string)GetField(machine, "m_initialState") == nameof(MiniGameUIClosedState), "Scene starts closed");
            Require(GetField(testUI, "m_uiRoot") != null, "Scene UI root reference resolves");
        }
        finally { if (openedForCheck) EditorSceneManager.CloseScene(scene, true); }
    }

    private static FieldInfo Field(object target, string name)
    {
        Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static object GetField(object target, string name) => Field(target, name).GetValue(target);
    private static void Invoke(object target, string name) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("MiniGameUIBaseChecks: " + message);
    }
}
