using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// ゲームサイクル確認用のデバッグ機能（エディターと開発ビルドのみ）
//
// ■ 画面表示（UI を作らなくても確認できる仮表示）
//   Title / Game / Result のどのシーンに置いても、
//   そのシーンに合わせた情報を画面に表示する
//     Title  : 「Press Enter / Start」
//     Game   : 状態、残り時間、各プレイヤーのスコア、
//              カウントダウン、START、TIME UP
//     Result : 順位表（順位 / プレイヤー / スコア）
//
// ■ キー操作（Game シーンのみ）
//   F1 ～ F4 : 1P ～ 4P に 100 点
//   F5       : 残り時間を 5 秒にする（すぐタイムアップを確認したいとき）
//   他のメンバーの数字キー操作とかぶらないように F キーを使っている
//
// 本番では外してよい
//==================================================
public class GAME_DEBUG_TOOL : MonoBehaviour
{
    [Header("キー操作")]
    [SerializeField]
    private int m_debugScore = 100;

    [Header("画面表示")]
    [SerializeField]
    private bool m_showHud = true;

    [Tooltip("文字の大きさ（画面の高さ 1080 のときの大きさ。画面サイズに合わせて自動で拡大縮小）")]
    [SerializeField]
    private int m_fontSize = 32;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    // 「START」を表示する時間
    const float START_TEXT_TIME = 1f;

    private GAME_TITLE m_title;
    private GAME_RESULT m_result;

    private GAME_MANAGER.GAME_STATE m_lastState;
    private float m_startTextTimer;

    private GUIStyle m_style;
    private GUIStyle m_bigStyle;
    private GUIStyle m_tableStyle;
    private float m_styleScale = -1f;

    void Start()
    {
        m_title = FindFirstObjectByType<GAME_TITLE>();
        m_result = FindFirstObjectByType<GAME_RESULT>();

        if (GAME_MANAGER.Instance != null)
        {
            m_lastState = GAME_MANAGER.Instance.State;
        }
    }

    //==================================================
    // キー操作
    //==================================================

    void Update()
    {
        UpdateStartText();

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f1Key.wasPressedThisFrame) AddDebugScore(0);
        if (keyboard.f2Key.wasPressedThisFrame) AddDebugScore(1);
        if (keyboard.f3Key.wasPressedThisFrame) AddDebugScore(2);
        if (keyboard.f4Key.wasPressedThisFrame) AddDebugScore(3);

        if (keyboard.f5Key.wasPressedThisFrame &&
            GAME_MANAGER.Instance != null)
        {
            GAME_MANAGER.Instance.SetRemainingTime(5f);
        }
    }

    void AddDebugScore(int playerIndex)
    {
        if (GAME_MANAGER.Instance == null)
        {
            return;
        }

        if (GAME_SCORE.IsLocked)
        {
            Debug.Log("【デバッグ】今は加点できません（プレイ中のみ加点されます）");
            return;
        }

        GAME_SCORE.AddScore(playerIndex, m_debugScore);

        Debug.Log(
            "【デバッグ】" + (playerIndex + 1) + "P に " + m_debugScore +
            " 点（" + (playerIndex + 1) + "P の合計 " + GAME_SCORE.GetPlayerScore(playerIndex) + "）");
    }

    // READY → PLAYING に変わった瞬間を見つけて「START」を表示する
    void UpdateStartText()
    {
        GAME_MANAGER manager = GAME_MANAGER.Instance;

        if (manager == null)
        {
            return;
        }

        if (m_lastState == GAME_MANAGER.GAME_STATE.READY &&
            manager.State == GAME_MANAGER.GAME_STATE.PLAYING)
        {
            m_startTextTimer = START_TEXT_TIME;
        }

        m_lastState = manager.State;

        if (m_startTextTimer > 0f)
        {
            m_startTextTimer -= Time.deltaTime;
        }
    }

    //==================================================
    // 画面表示
    //==================================================

    void OnGUI()
    {
        if (!m_showHud)
        {
            return;
        }

        // この表示はトランジションの画面より手前に描かれてしまうので、遷移中は出さない
        if (GAME_SCENE_LOADER.IsTransitioning)
        {
            return;
        }

        SetupStyles();

        if (GAME_MANAGER.Instance != null)
        {
            DrawGame(GAME_MANAGER.Instance);
        }
        else if (m_result != null)
        {
            DrawResult(m_result);
        }
        else if (m_title != null)
        {
            DrawTitle();
        }
    }

    void DrawTitle()
    {
        DrawInfo("[ TITLE ]");

        DrawCenter("Press Enter / Start", Color.white);
    }

    void DrawGame(GAME_MANAGER manager)
    {
        DrawInfo(
            "[ GAME ]  " + manager.State +
            (GAME_INTRO.IsPlaying ? "  (INTRO  Enter: skip)" : "") + "\n" +
            "TIME   " + FormatTime(manager.RemainingTime) + "\n" +
            GetPlayerScoresLine() + "\n" +
            "\n" +
            "F1-F4 : +" + m_debugScore + " (1P-4P)\n" +
            "F5    : 5 sec left");

        switch (manager.State)
        {
            case GAME_MANAGER.GAME_STATE.READY:
                // 開始演出中はカウントダウンを出さない（演出の邪魔にならないように）
                if (manager.IsWaitingForCountdown)
                {
                    break;
                }

                DrawCenter(
                    Mathf.CeilToInt(Mathf.Max(manager.ReadyRemaining, 0.01f)).ToString(),
                    Color.white);
                break;

            case GAME_MANAGER.GAME_STATE.PLAYING:
                if (m_startTextTimer > 0f)
                {
                    DrawCenter("START!", Color.yellow);
                }
                break;

            case GAME_MANAGER.GAME_STATE.TIME_UP:
                DrawCenter("TIME UP!", new Color(1f, 0.4f, 0.4f));
                break;
        }
    }

    void DrawResult(GAME_RESULT result)
    {
        string info = "[ RESULT ]";

        if (result.IsCountFinished)
        {
            info += "\n\nEnter / Start : back to title";
        }

        DrawInfo(info);

        DrawRankingTable(result);
    }

    // 画面中央に順位表（順位 / プレイヤー / スコア）を表示
    void DrawRankingTable(GAME_RESULT result)
    {
        IReadOnlyList<GAME_RESULT.RESULT_ENTRY> entries =
            result.Entries;

        float fontSize = m_tableStyle.fontSize;

        // 列の幅（文字の大きさに合わせて決める）
        float placeWidth = fontSize * 4f;
        float playerWidth = fontSize * 4f;
        float scoreWidth = fontSize * 6f;
        float rowHeight = fontSize * 1.6f;

        float tableWidth = placeWidth + playerWidth + scoreWidth;
        float tableHeight = rowHeight * (entries.Count + 1);

        float padding = fontSize * 0.6f;

        float left = (Screen.width - tableWidth) * 0.5f;
        float top = (Screen.height - tableHeight) * 0.5f;

        // 背景
        Color oldColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(
            new Rect(
                left - padding,
                top - padding,
                tableWidth + padding * 2f,
                tableHeight + padding * 2f),
            Texture2D.whiteTexture);

        GUI.color = oldColor;

        // 見出し
        DrawTableRow(
            left, top, placeWidth, playerWidth, scoreWidth, rowHeight,
            "RANK", "PLAYER", "SCORE",
            new Color(0.7f, 0.7f, 0.7f));

        // 各プレイヤー（1位は金色）
        for (int i = 0; i < entries.Count; i++)
        {
            GAME_RESULT.RESULT_ENTRY entry = entries[i];

            Color color =
                result.IsCountFinished && entry.m_place == 1
                    ? new Color(1f, 0.85f, 0.2f)
                    : Color.white;

            DrawTableRow(
                left, top + rowHeight * (i + 1),
                placeWidth, playerWidth, scoreWidth, rowHeight,
                GAME_RESULT.GetPlaceText(entry.m_place),
                (entry.m_playerIndex + 1) + "P",
                result.GetDisplayedScore(entry).ToString(),
                color);
        }
    }

    void DrawTableRow(
        float left, float top,
        float placeWidth, float playerWidth, float scoreWidth, float rowHeight,
        string place, string player, string score,
        Color color)
    {
        m_tableStyle.normal.textColor = color;

        m_tableStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(left, top, placeWidth, rowHeight), place, m_tableStyle);

        m_tableStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(left + placeWidth, top, playerWidth, rowHeight), player, m_tableStyle);

        m_tableStyle.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(left + placeWidth + playerWidth, top, scoreWidth, rowHeight), score, m_tableStyle);
    }

    //==================================================
    // 描画の補助
    //==================================================

    void SetupStyles()
    {
        float scale =
            Screen.height / 1080f;

        if (m_style != null &&
            Mathf.Approximately(scale, m_styleScale))
        {
            return;
        }

        m_styleScale = scale;

        m_style = new GUIStyle(GUI.skin.label);
        m_style.fontSize = Mathf.Max(10, Mathf.RoundToInt(m_fontSize * scale));
        m_style.fontStyle = FontStyle.Bold;
        m_style.normal.textColor = Color.white;
        m_style.alignment = TextAnchor.UpperLeft;
        m_style.wordWrap = false;

        m_bigStyle = new GUIStyle(m_style);
        m_bigStyle.fontSize = m_style.fontSize * 3;
        m_bigStyle.alignment = TextAnchor.MiddleCenter;

        m_tableStyle = new GUIStyle(m_style);
        m_tableStyle.fontSize = Mathf.RoundToInt(m_style.fontSize * 1.5f);
    }

    // 左上に情報を表示（読みやすいように半透明の黒い背景を敷く）
    void DrawInfo(string text)
    {
        GUIContent content = new GUIContent(text);

        Vector2 size =
            m_style.CalcSize(content);

        float padding =
            m_style.fontSize * 0.5f;

        Rect backRect =
            new Rect(
                padding,
                padding,
                size.x + padding * 2f,
                size.y + padding * 2f);

        Color oldColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(backRect, Texture2D.whiteTexture);

        GUI.color = oldColor;

        GUI.Label(
            new Rect(
                backRect.x + padding,
                backRect.y + padding,
                size.x,
                size.y),
            content,
            m_style);
    }

    // 画面中央に大きく表示
    // （影を少しずらして重ねると二重に見えるので、半透明の黒い帯を敷いて読みやすくする）
    void DrawCenter(string text, Color color)
    {
        GUIContent content = new GUIContent(text);

        Vector2 size =
            m_bigStyle.CalcSize(content);

        float padding =
            m_bigStyle.fontSize * 0.3f;

        Rect backRect =
            new Rect(
                (Screen.width - size.x) * 0.5f - padding,
                (Screen.height - size.y) * 0.5f - padding,
                size.x + padding * 2f,
                size.y + padding * 2f);

        Color oldColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(backRect, Texture2D.whiteTexture);

        GUI.color = oldColor;

        m_bigStyle.normal.textColor = color;

        GUI.Label(
            new Rect(0f, 0f, Screen.width, Screen.height),
            content,
            m_bigStyle);
    }

    static string GetPlayerScoresLine()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int i = 0; i < GAME_SCORE.MAX_PLAYERS; i++)
        {
            if (i > 0)
            {
                builder.Append("   ");
            }

            builder.Append(i + 1);
            builder.Append("P ");
            builder.Append(GAME_SCORE.GetPlayerScore(i));
        }

        return builder.ToString();
    }

    // GAME_MANAGER の表示と同じく切り上げ（0 になった瞬間に終わる）
    static string FormatTime(float time)
    {
        int seconds =
            Mathf.CeilToInt(Mathf.Max(time, 0f));

        return (seconds / 60).ToString() + ":" + (seconds % 60).ToString("00");
    }
#endif
}
