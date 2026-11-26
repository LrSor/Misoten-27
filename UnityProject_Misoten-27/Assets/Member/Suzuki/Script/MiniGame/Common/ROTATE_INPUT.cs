using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// Joy-Con を「回した」ことを調べる（回すミニゲームで共通の部品）
//
// ・ジャイロ（回転の速さ）から、1 秒に何回転しているか（回転の速さ）と、回した回数を出す
// ・回す速さを 4 段階で教えてくれる：NONE（回していない）/ SLOW / NORMAL / FAST
// ・Joy-Con を振動させられる（Rumble）
// ・ボタン（ZL など）を押しているか・離したかも調べられる（IsButtonHeld / ConsumeRelease()）
// ・「振り下ろし」も調べられる（ConsumeSwingDown()）。縦方向にグッと振ったとき
//   ※ 回している最中は回す動きでも反応してしまうので、使う側で「回し終わったあと」だけ見ること
//
// ■ 使い方
//   ・ミニゲーム本体（MiniGame）の「m_objects」に入れる（一番上に入れる）
//   ・本体からは ConsumeTurns() で、前回から増えた回転数（1 周 = 1）を受け取る
//
// ■ キーボードで試すとき（Joy-Con がないとき）
//   W → D → S → A の順に押すと 1 回転（髙橋さんの回すミニゲームと同じ）
//   スペースキー = ボタン（押している / 離した）
//   ↓キー = 振り下ろし
//
// ・通信のときは、髙橋さんの MiniGameObject の NetworkInput（ジャイロ・ボタン）を使う
//   （振動は自分の PC の Joy-Con だけ）
//==================================================
public class ROTATE_INPUT : MiniGameObject
{
    public enum AXIS
    {
        ANY,    // どの向きに回しても数える（おすすめ）
        X,
        Y,
        Z,
    }

    public enum SPEED_LEVEL
    {
        NONE,       // 回していない・遅すぎる
        SLOW,
        NORMAL,
        FAST,
    }

    [Header("回転の判定")]
    [Tooltip("どの軸の回転を数えるか。ANY ならどの向きに回しても数える")]
    [SerializeField]
    private AXIS m_axis = AXIS.ANY;

    [Tooltip("これより遅い回転（rad/秒）は、手のふるえとして無視する")]
    [SerializeField]
    private float m_deadZone = 1.0f;

    [Header("速さの段階（1 秒あたりの回転数）")]
    [Tooltip("これより遅いと NONE（アクション指示が出る）")]
    [SerializeField]
    private float m_slowRate = 0.5f;

    [Tooltip("これ以上で NORMAL")]
    [SerializeField]
    private float m_normalRate = 1.2f;

    [Tooltip("これ以上で FAST")]
    [SerializeField]
    private float m_fastRate = 2.0f;

    [Tooltip("速さの変化のなめらかさ（大きいほどすぐ変わる）")]
    [SerializeField]
    private float m_rateSmoothness = 6f;

    [Header("ボタン")]
    [Tooltip("ボタンを使う")]
    [SerializeField]
    private bool m_useButton = false;

    [Tooltip("使う Joy-Con のボタン（ZL = SHOULDER_2）")]
    [SerializeField]
    private Joycon.Button m_button = Joycon.Button.SHOULDER_2;

    [Tooltip("キーボードで試すときのキー")]
    [SerializeField]
    private Key m_keyboardButton = Key.Space;

    [Header("振り下ろし")]
    [Tooltip("振り下ろしを調べる")]
    [SerializeField]
    private bool m_useSwingDown = false;

    [Tooltip("縦方向にこの強さ（G）を超えたら「振り下ろした」とする。小さくすると軽く振っても反応する")]
    [SerializeField]
    private float m_swingDownThreshold = 1.5f;

    [Tooltip("振り下ろしを続けて数えないための間隔（秒）")]
    [SerializeField]
    private float m_swingDownInterval = 0.4f;

    [Tooltip("キーボードで試すときの、振り下ろしのキー")]
    [SerializeField]
    private Key m_keyboardSwingDown = Key.DownArrow;

    [Header("確認用")]
    [Tooltip("画面に数値を出す（調整用）")]
    [SerializeField]
    private bool m_showDebug = false;

    //============================================================
    // JoyCon関連
    //============================================================
    private List<Joycon> m_joycons;
    private Joycon m_joycon;

    private float m_turns;          // まだ受け取られていない回転数
    private float m_totalTurns;
    private float m_turnRate;       // 1 秒あたりの回転数（なめらかにしたもの）
    private SPEED_LEVEL m_level = SPEED_LEVEL.NONE;
    private bool m_isActive;

    // ボタン
    private bool m_isButtonHeld;
    private bool m_isReleased;

    // 振り下ろし
    private Vector3 m_gravity;
    private bool m_hasGravity;
    private bool m_isSwungDown;
    private float m_lastSwingDownTime = -999f;
    private float m_debugVerticalPeak;

    // キーボード用
    private int m_keyboardStep;
    private GUIStyle m_debugStyle;

    public SPEED_LEVEL Level { get { return m_level; } }
    public float TurnRate { get { return m_turnRate; } }
    public float TotalTurns { get { return m_totalTurns; } }
    public bool IsActive { get { return m_isActive; } }
    public bool IsIdle { get { return m_isActive && m_level == SPEED_LEVEL.NONE; } }

    // ボタンを押しているか
    public bool IsButtonHeld { get { return m_isButtonHeld; } }

    // 速さを 0～1 で（演出用）
    public float Speed01
    {
        get { return Mathf.Clamp01(m_turnRate / Mathf.Max(0.01f, m_fastRate)); }
    }

    //オブジェクト初期化関数
    public override void Activate()
    {
        m_turns = 0f;
        m_totalTurns = 0f;
        m_turnRate = 0f;
        m_level = SPEED_LEVEL.NONE;
        m_keyboardStep = 0;
        m_isButtonHeld = false;
        m_isReleased = false;
        m_hasGravity = false;
        m_isSwungDown = false;
        m_lastSwingDownTime = -999f;
        m_isActive = true;
        m_joycon = null;

        // 通信のときは NetworkInput を使う
        if (IsNetworkControlled)
        {
            return;
        }

        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
        JoyconManager joyconManager = FindFirstObjectByType<JoyconManager>();
        if (joyconManager == null ||
            joyconManager.GetIsKeyboardPlay() ||
            JoyconManager.Instance == null)
        {
            return;
        }

        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joycon = m_joycons.First();
        //============================================================
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        m_joycon = null;
        m_isActive = false;
        m_turnRate = 0f;
        m_level = SPEED_LEVEL.NONE;
        m_isButtonHeld = false;
        m_isReleased = false;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        float deltaTime = Time.deltaTime;
        float rate;
        bool isHeld;

        if (IsNetworkControlled || m_joycon != null)
        {
            Vector3 gyro = IsNetworkControlled ? NetworkInput.m_gyro : m_joycon.GetGyro();
            rate = GetAngularSpeed(gyro) / (Mathf.PI * 2f);
            isHeld = IsNetworkControlled ? NetworkInput.m_button : m_joycon.GetButton(m_button);

            if (m_useSwingDown)
            {
                Vector3 accel = IsNetworkControlled ? NetworkInput.m_acceleration : m_joycon.GetAccel();
                DetectSwingDown(accel, deltaTime);
            }
        }
        else
        {
            //デバッグ用キーボード操作
            rate = UpdateKeyboard(deltaTime);
            isHeld = Keyboard.current != null && Keyboard.current[m_keyboardButton].isPressed;

            if (m_useSwingDown &&
                Keyboard.current != null &&
                Keyboard.current[m_keyboardSwingDown].wasPressedThisFrame)
            {
                AddSwingDown();
            }
        }

        float turns = rate * deltaTime;
        m_turns += turns;
        m_totalTurns += turns;

        m_turnRate = Mathf.Lerp(m_turnRate, rate, 1f - Mathf.Exp(-deltaTime * m_rateSmoothness));

        UpdateLevel();
        UpdateButton(isHeld);
    }

    // 前回呼んだときから増えた回転数（1 周 = 1）を受け取る
    public float ConsumeTurns()
    {
        float turns = m_turns;
        m_turns = 0f;
        return turns;
    }

    // 前回呼んだときから、ボタンを離したか（1 回離すと 1 回だけ true）
    public bool ConsumeRelease()
    {
        bool isReleased = m_isReleased;
        m_isReleased = false;
        return isReleased;
    }

    // 前回呼んだときから、振り下ろしたか（1 回振り下ろすと 1 回だけ true）
    public bool ConsumeSwingDown()
    {
        bool isSwungDown = m_isSwungDown;
        m_isSwungDown = false;
        return isSwungDown;
    }

    // 縦方向（重力の向き）の加速度で、振り下ろしを調べる
    void DetectSwingDown(Vector3 accel, float deltaTime)
    {
        if (!m_hasGravity)
        {
            m_gravity = accel;
            m_hasGravity = true;
            return;
        }

        // 重力の向き：加速度をゆっくり追いかける
        m_gravity = Vector3.Lerp(m_gravity, accel, 1f - Mathf.Exp(-deltaTime / 0.3f));

        if (m_gravity.sqrMagnitude < 0.01f)
        {
            return;
        }

        // 振った動きのうち、縦方向の強さ
        float vertical = Mathf.Abs(Vector3.Dot(accel - m_gravity, m_gravity.normalized));
        m_debugVerticalPeak = Mathf.Max(vertical, m_debugVerticalPeak - deltaTime * 2f);

        if (vertical > m_swingDownThreshold)
        {
            AddSwingDown();
        }
    }

    void AddSwingDown()
    {
        if (Time.time - m_lastSwingDownTime < m_swingDownInterval)
        {
            return;
        }

        m_lastSwingDownTime = Time.time;
        m_isSwungDown = true;
    }

    // Joy-Con を振動させる（自分の PC の Joy-Con だけ）
    //   strength：強さ（0～1）、seconds：長さ（秒）
    public void Rumble(float strength = 0.6f, float seconds = 0.4f)
    {
        if (m_joycon == null)
        {
            return;
        }

        m_joycon.SetRumble(160f, 320f, Mathf.Clamp01(strength), Mathf.RoundToInt(seconds * 1000f));
    }

    void UpdateButton(bool isHeld)
    {
        if (!m_useButton)
        {
            m_isButtonHeld = false;
            return;
        }

        // 押していた → 離した
        if (m_isButtonHeld && !isHeld)
        {
            m_isReleased = true;
        }

        m_isButtonHeld = isHeld;
    }

    // 回転の速さ（rad/秒）。ふるえ（デッドゾーン）より遅い分は 0
    float GetAngularSpeed(Vector3 gyro)
    {
        float speed;

        switch (m_axis)
        {
            case AXIS.X:
                speed = Mathf.Abs(gyro.x);
                break;

            case AXIS.Y:
                speed = Mathf.Abs(gyro.y);
                break;

            case AXIS.Z:
                speed = Mathf.Abs(gyro.z);
                break;

            default:
                speed = gyro.magnitude;
                break;
        }

        return Mathf.Max(0f, speed - m_deadZone);
    }

    // W → D → S → A の順に押すと 1 回転（1 つ進むごとに 1/4 回転）
    float UpdateKeyboard(float deltaTime)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return 0f;
        }

        Key[] order = { Key.W, Key.D, Key.S, Key.A };

        float added = 0f;

        if (keyboard[order[m_keyboardStep]].wasPressedThisFrame)
        {
            m_keyboardStep = (m_keyboardStep + 1) % order.Length;
            added = 0.25f;
        }

        // 押した分を、その 1 フレームの速さとして返す
        return deltaTime > 0f ? added / deltaTime : 0f;
    }

    void UpdateLevel()
    {
        if (m_turnRate >= m_fastRate)
        {
            m_level = SPEED_LEVEL.FAST;
        }
        else if (m_turnRate >= m_normalRate)
        {
            m_level = SPEED_LEVEL.NORMAL;
        }
        else if (m_turnRate >= m_slowRate)
        {
            m_level = SPEED_LEVEL.SLOW;
        }
        else
        {
            m_level = SPEED_LEVEL.NONE;
        }
    }

    void OnValidate()
    {
        m_deadZone = Mathf.Max(0f, m_deadZone);
        m_normalRate = Mathf.Max(m_slowRate, m_normalRate);
        m_fastRate = Mathf.Max(m_normalRate, m_fastRate);
        m_rateSmoothness = Mathf.Max(0.1f, m_rateSmoothness);
    }

    void OnGUI()
    {
        if (!m_showDebug || !m_isActive)
        {
            return;
        }

        if (m_debugStyle == null)
        {
            m_debugStyle = new GUIStyle(GUI.skin.label);
            m_debugStyle.fontSize = 22;
            m_debugStyle.normal.textColor = Color.white;
        }

        Rect area = new Rect(Screen.width - 640f, (Screen.height - 220f) * 0.5f, 620f, 220f);

        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = before;

        GUI.Label(new Rect(area.x + 12f, area.y + 8f, area.width - 24f, area.height - 16f),
            "【ROTATE_INPUT】\n" +
            "入力：" + (IsNetworkControlled ? "通信" : m_joycon != null ? "Joy-Con" : "キーボード（W→D→S→A）") + "\n" +
            "速さ：" + m_level + "（" + m_turnRate.ToString("0.0") + " 回転/秒）\n" +
            "回した回数：" + m_totalTurns.ToString("0.0") +
            (m_useButton ? "\nボタン：" + (m_isButtonHeld ? "押している" : "離している") : "") +
            (m_useSwingDown ? "\n振り下ろし（縦の強さ・最近の最大）：" + m_debugVerticalPeak.ToString("0.0") + " / " + m_swingDownThreshold.ToString("0.0") + " G" : ""),
            m_debugStyle);
    }
}
