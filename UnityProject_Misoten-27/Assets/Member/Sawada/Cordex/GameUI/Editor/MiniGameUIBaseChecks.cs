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
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation && !ui.CanControlPlayer,
                "Explanation locks game controls");
            Require(!control.enabled && !disabledControl.enabled && machine.enabled && root.activeSelf,
                "Only configured player controls are suspended");
            Require(Mathf.Abs(ui.RemainingSeconds - 4.6f) < 0.01f, "Explanation sequence is 4.6 seconds");
            Require(callIcon.rectTransform.localScale == Vector3.zero && bubble.localScale == Vector3.zero,
                "Explanation starts with both scales at zero");
            ui.BeginMiniGame();
            ui.FinishMiniGame();
            ui.ReceiveVector2Input(Vector2.one);
            ui.ReceiveButtonInput(true);
            Require(playingState.Vector2Input == Vector2.zero && !playingState.IsButtonPressed, "Explanation ignores input");
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation, "Repeated start and early finish are ignored");
            CheckAnimation(ui, explanation, callIcon, bubble, 0.1f, 0.5f, 1f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 0.2f, 1f, 1f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 0.4f, 1f, 0.5f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 0.6f, 1f, 1f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 0.8f, 1f, 0.5f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 1.0f, 1f, 1f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 1.2f, 1f, 0.5f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 1.4f, 1f, 0f, 0f);
            CheckAnimation(ui, explanation, callIcon, bubble, 1.45f, 1f, 0f, 0.5f);
            CheckAnimation(ui, explanation, callIcon, bubble, 1.5f, 1f, 0f, 1f);
            CheckAnimation(ui, explanation, callIcon, bubble, 4.49f, 1f, 0f, 1f);
            CheckAnimation(ui, explanation, callIcon, bubble, 4.55f, 1f, 0f, 0.5f);
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 4.61f);
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
            Require(callIcon.rectTransform.localScale == Vector3.zero && bubble.localScale == Vector3.zero &&
                Mathf.Approximately(callIcon.color.a, 1f), "Interruption resets animation");
            ui.BeginMiniGame();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation, "Can restart after interruption");
            ui.CancelMiniGame();
            var sequenceObject = new GameObject("Speech sequence", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sequenceObject.transform.SetParent(root.transform);
            var sequence = sequenceObject.AddComponent<UISpriteSequencePlayer>();
            SetField(sequence, "m_spriteSheet", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Member/Sawada/Texture/TestRenban.png"));
            Invoke(sequence, "OnValidate");
            SetField(explanation, "m_speechAnimation", sequence);
            ui.BeginMiniGame();
            Require(Mathf.Abs(ui.RemainingSeconds - 3.6f) < 0.01f, "Estimated duration follows sequence duration");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 1.5f);
            explanation.UpdateState();
            Require(sequence.IsPlaying, "Sequence begins when bubble finishes opening");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 20f);
            explanation.UpdateState();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation && bubble.localScale == Vector3.one,
                "Elapsed time alone cannot close bubble while sequence is unfinished");
            typeof(UISpriteSequencePlayer).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sequence, new object[] { 2f });
            Require(sequence.HasCompleted && sequence.CurrentFrameIndex == 99, "Explanation plays sequence once even with Loop on");
            explanation.UpdateState();
            Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation, "Completion starts closing before play");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 20.05f);
            explanation.UpdateState();
            Require(Mathf.Abs(bubble.localScale.x - 0.5f) < 0.002f, "Bubble closes over 0.1 seconds after completion");
            SetField(ui, "m_phaseStartedAt", Time.unscaledTime - 20.11f);
            explanation.UpdateState();
            Require(ui.CanControlPlayer && !sequence.IsPlaying && !sequence.HasCompleted,
                "Play begins only after closing and playback resets");
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

    private static void CheckAnimation(MiniGameUIBase ui, MiniGameUIExplanationState state,
        Image callIcon, RectTransform bubble, float elapsed, float callScale, float alpha, float bubbleScale)
    {
        SetField(ui, "m_phaseStartedAt", Time.unscaledTime - elapsed);
        state.UpdateState();
        Require(ui.CurrentPhase == MiniGameUIBase.Phase.Explanation && !ui.CanControlPlayer,
            "Controls locked until bubble has closed at " + elapsed);
        Require(Vector3.Distance(callIcon.rectTransform.localScale, Vector3.one * callScale) < 0.002f &&
            Mathf.Abs(callIcon.color.a - alpha) < 0.002f &&
            Vector3.Distance(bubble.localScale, Vector3.one * bubbleScale) < 0.002f,
            "Animation values at " + elapsed);
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
            Require(GetField(explanation, "m_speechAnimation") is UISpriteSequencePlayer,
                "Scene sprite sequence is connected to explanation State");
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
