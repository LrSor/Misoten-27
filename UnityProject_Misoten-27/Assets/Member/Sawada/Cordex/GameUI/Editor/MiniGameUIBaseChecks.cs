using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI の遷移・操作復帰・Scene 参照を、Scene を保存せずに確認する。</summary>
public static class MiniGameUIBaseChecks
{
    [MenuItem("Tools/Sawada/Check MiniGame UI Base")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run MiniGame UI checks in Edit Mode.");
        CheckSpriteSequence();
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
            var callObject = new GameObject("CallIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            callObject.transform.SetParent(root.transform);
            Image callIcon = callObject.GetComponent<Image>();
            var bubbleObject = new GameObject("SpeechBubble", typeof(RectTransform));
            bubbleObject.transform.SetParent(root.transform);
            var bubble = bubbleObject.GetComponent<RectTransform>();
            var explanation = owner.GetComponent<MiniGameUIExplanationState>();
            SetField(explanation, "m_callIcon", callIcon);
            SetField(explanation, "m_speechBubble", bubble);
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
            Require(!control.enabled && !disabledControl.enabled && root.activeSelf, "Countdown locks controls");
            ui.ReceiveButtonInput(true);
            ui.FinishMiniGame(3);
            Require(!playingState.IsButtonPressed && ui.ResultStars == 0, "Countdown rejects clear input");
            Require(ui.ShowingReady && !ui.CanControlPlayer, "READY locks input");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 2f);
            explanation.UpdateState();
            Require(ui.ShowingStart && !ui.CanControlPlayer, "START locks input");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 3f);
            explanation.UpdateState();
            Require(ui.CanControlPlayer && control.enabled && !disabledControl.enabled, "Play restores previous controls");
            // 値を丸めずに渡すため、マウス delta など 1 を超える入力も受け取れる。
            Vector2 sample = new Vector2(12f, -3f);
            ui.ReceiveVector2Input(sample);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == sample && playingState.IsButtonPressed, "Function input reaches playing State");
            ui.ReceiveButtonInput(false);
            Require(!playingState.IsButtonPressed, "Button release reaches playing State");
            ui.ReceiveButtonInput(true);

            ui.FinishMiniGame(3);
            Require(ui.ResultStars == 3, "Uses the stars supplied by the game");
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
            Require(!ui.IsRunning && control.enabled && ui.ResultStars == 0, "Interruption resets UI");
            CheckExternalAuthority(ui, control);
            CheckScene();
            Debug.Log("MiniGameUIBaseChecks: PASS (timing, transitions, control restoration, restart, external authority, scene references)");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    private static void CheckExternalAuthority(MiniGameUIBase ui, Camera control)
    {
        ui.SetExternalControl(true);
        ui.BeginMiniGame();
        Require(!ui.IsRunning, "External authority blocks local test start");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Countdown, 0f, 3f);
        ui.GetComponent<MiniGameUIExplanationState>().UpdateState();
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Countdown, "Countdown waits for Host even at zero");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Playing, 60f, 60f);
        Require(ui.RemainingSeconds > 59f && control.enabled, "Host timer replaces ten-second timer without changing controls");
        SetField(ui, "m_externalSampleAt", Time.time - 5f);
        ui.GetComponent<MiniGameUIPlayingState>().UpdateState();
        Require(ui.RemainingSeconds == 60f, "UI does not decrement a supplied time sample");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Playing, 23.25f, 60f);
        Require(ui.RemainingSeconds == 23.25f, "Next game time sample overwrites the display value");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Playing, 0f, 0f, hasTimer: false);
        SetField(ui, "m_externalSampleAt", Time.time - 60f);
        ui.GetComponent<MiniGameUIPlayingState>().UpdateState();
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Playing && ui.RemainingSeconds == 0f,
            "No timer stays still and does not end the game");
        ui.ReceiveButtonInput(true);
        ui.FinishMiniGame(2);
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Playing
            && !ui.GetComponent<MiniGameUIPlayingState>().IsButtonPressed, "External mode blocks test clear input");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Playing, 0f, 60f);
        ui.GetComponent<MiniGameUIPlayingState>().UpdateState();
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Playing, "UI timeout cannot decide the game result");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Result, 0f, .5f, 3, true);
        Require(ui.ResultStars == 3, "Time-based score games retain Host-confirmed stars at zero");
        object startedAt = GetField(ui, "m_phaseStartedAt");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Result, 0f, .5f, 3, true);
        Require(startedAt.Equals(GetField(ui, "m_phaseStartedAt")), "Repeated snapshots do not restart the result state");
        ui.GetComponent<MiniGameUIResultState>().UpdateState();
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Result, "Result waits for Host completion");
        // Animation itself requires Play Mode; exercise the completion transition without spawning sprites.
        SetField(ui, "m_star", null);
        ui.CloseExternalGame(true);
        ui.CloseExternalGame(true);
        Require(!ui.IsRunning && ui.ResultStars == 0, "Completion is safe to repeat");
        ui.ApplyExternalPhase(MiniGameUIBase.Phase.Countdown, 3f, 3f);
        ui.CloseExternalGame(false);
        Require(!ui.IsRunning && control.enabled, "Cancellation closes presentation and preserves controls");
        ui.SetExternalControl(false);
        ui.BeginMiniGame();
        Require(ui.IsRunning && !control.enabled, "Standalone behavior remains available after unbinding");
        ui.CancelMiniGame();
    }

    private static void CheckSpriteSequence()
    {
        var owner = new GameObject("Sprite sequence check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        try
        {
            var player = owner.AddComponent<UISpriteSequencePlayer>();
            SetField(player, "m_spriteSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Member/Sawada/Texture/TestRenban.png"));
            Invoke(player, "OnValidate");
            var frames = (Sprite[])GetField(player, "m_frames");
            Require(frames.Length == 100 && frames[2].name == "sample_2" && frames[10].name == "sample_10",
                "TestRenban contains 100 frames in numeric order");
            var advance = typeof(UISpriteSequencePlayer).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            SetField(player, "m_durationSeconds", 2f);
            player.Play();
            advance.Invoke(player, new object[] { 1f });
            Require(player.CurrentFrameIndex == 50 && owner.GetComponent<Image>().sprite == frames[50],
                "Half of specified duration displays middle frame");
            advance.Invoke(player, new object[] { 1f });
            Require(player.CurrentFrameIndex == 0 && player.IsPlaying, "Loop returns to first frame after two seconds");
            advance.Invoke(player, new object[] { 2.5f });
            Require(player.CurrentFrameIndex == 25, "Long frame preserves playback timing");
            SetField(player, "m_loop", false);
            player.Play();
            advance.Invoke(player, new object[] { 2f });
            Require(player.CurrentFrameIndex == 99 && !player.IsPlaying && player.HasCompleted,
                "Nonlooping sequence ends on final sprite and signals completion");
            foreach (int count in new[] { 5, 6 })
            {
                var shorter = new Sprite[count];
                Array.Copy(frames, shorter, count);
                SetField(player, "m_frames", shorter);
                player.Play();
                advance.Invoke(player, new object[] { 1f });
                Require(player.CurrentFrameIndex == count / 2, "Different frame counts share the duration");
            }
            player.Stop();
            Require(player.CurrentFrameIndex == 0 && !player.IsPlaying && !player.HasCompleted, "Stop resets sequence");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
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
            var explanation = testUI.GetComponent<MiniGameUIExplanationState>();
            Require(GetField(explanation, "m_callIcon") is Image, "Scene CallIcon reference resolves");
            Require(GetField(explanation, "m_speechBubble") is RectTransform, "Scene SpeechBubble reference resolves");
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
