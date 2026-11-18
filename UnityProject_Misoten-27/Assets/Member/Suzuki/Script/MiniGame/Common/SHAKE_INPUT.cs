using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// Joy-Con を「振った」ことを調べる（振るミニゲーム 3 つで共通の部品）
//
// ・1 回振る（向きが切り替わる）たびに点数がたまる
//     まっすぐ振れたら 1 点、ブレていたら 0.5 点（数値は Inspector で変えられる）
// ・振る速さを 4 段階で教えてくれる：NONE（振っていない）/ SLOW / NORMAL / FAST
// ・まっすぐさ（0～1）も教えてくれる。演出（キラキラ・集中線）に使う
//
// ■ 使い方
//   ・ミニゲーム本体（MiniGame）の「m_objects」に入れる（一番上に入れる）
//   ・本体からは ConsumePoints() で、前回から増えた点数を受け取る
//
// ■ キーボードで試すとき（Joy-Con がないとき）
//   W と S を交互に押す = 上下に振る
//   左 Shift を押しながら = ブレている扱い
//
// ・高橋さんのテンプレート（Template_UseJoycon_MiniGameObject）をもとにしている
//==================================================
public class SHAKE_INPUT : MiniGameObject
{
    public enum AXIS
    {
        X,
        Y,
        Z,
        GRAVITY,    // 重力の向き（Joy-Con をどう持っていても「上下」に振った動き）
    }

    public enum ANY_SOURCE
    {
        GYRO,
        ACCEL,
        GYRO_OR_ACCEL,
    }

    public enum SPEED_LEVEL
    {
        NONE,       // 振っていない・弱い（アクション指示を出す）
        SLOW,
        NORMAL,
        FAST,
    }

    [Header("振りの判定")]
    [Tooltip("オン：決まった向き（上下）に振ったときだけ数える\nオフ：どの向きに振っても数える（落書きクリーンアップ用）")]
    [SerializeField]
    private bool m_useDirection = true;

    [Tooltip("振る向きの軸。Joy-Con を縦に持って上下に振るなら X（長い方向）\nGRAVITY にすると、持ち方に関係なく「上下」に振った動きを数える")]
    [SerializeField]
    private AXIS m_swingAxis = AXIS.X;

    [Tooltip("振る向きが GRAVITY のとき、まっすぐ持てているかを調べる軸\n（正しく持ったときに、上下を向く Joy-Con の軸。確認用の表示の「重力」で一番大きい軸）")]
    [SerializeField]
    private AXIS m_holdAxis = AXIS.Y;

    [Tooltip("この強さ（G）を超えたら「振った」とする。小さくすると軽く振っても反応する")]
    [SerializeField]
    private float m_swingThreshold = 1.0f;

    [Tooltip("向きを問わないとき、何で振ったことを調べるか\nGYRO：回転の速さ（モップのように手首・腕をふる動きに強い）\nACCEL：加速度の大きさ\nGYRO_OR_ACCEL：どちらか強い方")]
    [SerializeField]
    private ANY_SOURCE m_anySource = ANY_SOURCE.GYRO;

    [Tooltip("向きを問わないとき（GYRO）、この回転の速さ（rad/秒）を超えたら「振った」とする。小さくすると軽く振っても反応する")]
    [SerializeField]
    private float m_gyroThreshold = 4f;

    [Tooltip("連続で数えないための最短の間隔（秒）")]
    [SerializeField]
    private float m_minInterval = 0.08f;

    [Header("まっすぐさ")]
    [Tooltip("まっすぐさを点数に使う（オフならいつも 1 回 = まっすぐの点数）")]
    [SerializeField]
    private bool m_useStraightness = true;

    [Tooltip("縦からのズレがこの角度（度）までなら、まっすぐ")]
    [SerializeField]
    private float m_straightAngle = 20f;

    [Tooltip("縦からのズレがこの角度（度）以上なら、完全にブレている")]
    [SerializeField]
    private float m_bentAngle = 50f;

    [Tooltip("まっすぐさ（0～1）がこれ以上なら「まっすぐ」とする")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_straightBorder = 0.6f;

    [Header("点数")]
    [Tooltip("まっすぐ振れたときの点数")]
    [SerializeField]
    private float m_straightPoint = 1f;

    [Tooltip("ブレていたときの点数")]
    [SerializeField]
    private float m_bentPoint = 0.5f;

    [Header("速さの段階（1 秒あたりに振った回数）")]
    [Tooltip("これより少ないと NONE（アクション指示が出る）")]
    [SerializeField]
    private float m_slowRate = 1.5f;

    [Tooltip("これ以上で NORMAL")]
    [SerializeField]
    private float m_normalRate = 3f;

    [Tooltip("これ以上で FAST")]
    [SerializeField]
    private float m_fastRate = 5f;

    [Tooltip("何秒間の回数で速さを計るか")]
    [SerializeField]
    private float m_rateWindow = 1f;

    [Header("確認用")]
    [Tooltip("画面の左上に数値を出す（調整用）")]
    [SerializeField]
    private bool m_showDebug = false;

    [Tooltip("数値を出す場所（ほかの表示と重なるときに変える）")]
    [SerializeField]
    private DEBUG_POSITION m_debugPosition = DEBUG_POSITION.RIGHT_MIDDLE;

    public enum DEBUG_POSITION
    {
        RIGHT_MIDDLE,
        LEFT_MIDDLE,
        RIGHT_BOTTOM,
        LEFT_BOTTOM,
    }

    private GUIStyle m_debugStyle;

    //============================================================
    // JoyCon関連
    //============================================================
    private List<Joycon> m_joycons;
    private Joycon m_joycon;

    // 重力の向き（加速度をなめらかにしたもの）
    private Vector3 m_gravity;
    private bool m_hasGravity;

    // 振りの途中の状態
    private int m_lastDirection;        // 最後に数えた向き（+1 / -1）
    private bool m_isReleased = true;   // 向きを問わないとき：一度弱まったか
    private Vector3 m_lastSwingDirection; // 向きを問わないとき：今の振りの回転の向き
    private float m_debugGyroPeak;      // 確認用：最近の回転の速さの最大
    private float m_debugAccelPeak;     // 確認用：最近の加速度（重力をのぞく）の最大
    private float m_peakAxis;           // 今の振りで一番強かった軸方向の強さ
    private float m_peakRatio;          // そのときの「軸方向の割合」
    private float m_lastSwingTime = -999f;

    // 結果
    private float m_points;             // まだ受け取られていない点数
    private float m_totalPoints;
    private int m_swingCount;
    private float m_straightness = 1f;  // 今のまっすぐさ（0～1）
    private float m_holdStraightness = 1f;
    private float m_swingRate;
    private SPEED_LEVEL m_level = SPEED_LEVEL.NONE;
    private bool m_isActive;

    private readonly Queue<float> m_swingTimes = new Queue<float>();

    // キーボード用
    private int m_keyboardDirection;

    public SPEED_LEVEL Level { get { return m_level; } }
    public float Straightness { get { return m_straightness; } }
    public bool IsStraight { get { return !m_useStraightness || m_straightness >= m_straightBorder; } }
    public float SwingRate { get { return m_swingRate; } }
    public int SwingCount { get { return m_swingCount; } }
    public float TotalPoints { get { return m_totalPoints; } }
    public bool IsActive { get { return m_isActive; } }

    // 振っていない・弱いとき（アクション指示を出すとき）
    public bool IsIdle { get { return m_isActive && m_level == SPEED_LEVEL.NONE; } }

    // 速さを 0～1 で（演出用）
    public float Speed01
    {
        get { return Mathf.Clamp01(m_swingRate / Mathf.Max(0.01f, m_fastRate)); }
    }

    // 1 回振ったときの通知（引数は点数）
    public event System.Action<float> Swung;

    //オブジェクト初期化関数
    public override void Activate()
    {
        //メンバ変数の初期化などは、ジョイコンの初期化より先に行う
        ResetState();
        m_isActive = true;

        m_joycon = null;

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
        m_level = SPEED_LEVEL.NONE;
        m_swingRate = 0f;
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        if (m_joycon == null)
        {
            //デバッグ用キーボード操作
            UpdateKeyboard();
        }
        else
        {
            //ジョイコンを用いた操作
            UpdateJoycon();
        }

        UpdateLevel();
    }

    // 前回呼んだときから増えた点数を受け取る
    public float ConsumePoints()
    {
        float points = m_points;
        m_points = 0f;
        return points;
    }

    void ResetState()
    {
        m_hasGravity = false;
        m_lastDirection = 0;
        m_isReleased = true;
        m_lastSwingDirection = Vector3.zero;
        m_peakAxis = 0f;
        m_peakRatio = 1f;
        m_lastSwingTime = -999f;
        m_points = 0f;
        m_totalPoints = 0f;
        m_swingCount = 0;
        m_straightness = 1f;
        m_holdStraightness = 1f;
        m_swingRate = 0f;
        m_level = SPEED_LEVEL.NONE;
        m_swingTimes.Clear();
        m_keyboardDirection = 0;
    }

    //==================================================
    // Joy-Con
    //==================================================
    void UpdateJoycon()
    {
        Vector3 accel = m_joycon.GetAccel();

        //ジョイコンが R の場合、加速度 Y と Z の向きをそろえる（高橋さんの RotateObject と同じ）
        if (!m_joycon.isLeft)
        {
            accel.y = -accel.y;
            accel.z = -accel.z;
        }

        // 重力の向き：加速度をゆっくり追いかける（振っている揺れは平均すると消える）
        if (!m_hasGravity)
        {
            m_gravity = accel;
            m_hasGravity = true;
        }
        else
        {
            m_gravity = Vector3.Lerp(m_gravity, accel, 1f - Mathf.Exp(-Time.deltaTime / 0.4f));
        }

        // 持っている角度のまっすぐさ（重力が軸の向きに近いほど縦にまっすぐ）
        if (m_gravity.sqrMagnitude > 0.01f)
        {
            float cos = Mathf.Abs(GetAxis(m_gravity.normalized, GetHoldAxis()));
            float angle = Mathf.Acos(Mathf.Clamp01(cos)) * Mathf.Rad2Deg;
            m_holdStraightness = 1f - Mathf.InverseLerp(m_straightAngle, m_bentAngle, angle);
        }

        // 振った動きだけ（重力を引いたもの）
        Vector3 motion = accel - m_gravity;

        if (m_useDirection)
        {
            DetectDirectional(motion);
        }
        else
        {
            DetectAnyDirection(accel, m_joycon.GetGyro());
        }

        // 振っていないときも、持ち方のまっすぐさは演出に反映する
        if (Time.time - m_lastSwingTime > 0.5f)
        {
            m_straightness = Mathf.MoveTowards(m_straightness, m_holdStraightness, Time.deltaTime * 2f);
        }
    }

    // 上下に振ったとき：軸方向の強さが +しきい値 / -しきい値 を交互に超えたら 1 回
    void DetectDirectional(Vector3 motion)
    {
        float axis = GetSwing(motion);
        float size = motion.magnitude;

        // 振りの途中で一番強かったところの「まっすぐ度」を覚えておく
        if (Mathf.Abs(axis) > Mathf.Abs(m_peakAxis))
        {
            m_peakAxis = axis;
            m_peakRatio = size > 0.001f ? Mathf.Abs(axis) / size : 1f;
        }

        int direction = 0;

        if (axis > m_swingThreshold)
        {
            direction = 1;
        }
        else if (axis < -m_swingThreshold)
        {
            direction = -1;
        }

        if (direction != 0 &&
            direction != m_lastDirection &&
            Time.time - m_lastSwingTime >= m_minInterval)
        {
            m_lastDirection = direction;

            // 動きのまっすぐさ（軸の向きの割合が 0.5 以下ならブレ、0.9 以上ならまっすぐ）
            float motionStraight = Mathf.InverseLerp(0.5f, 0.9f, m_peakRatio);

            AddSwing(Mathf.Min(m_holdStraightness, motionStraight));

            m_peakAxis = 0f;
            m_peakRatio = 1f;
        }
    }

    // どの向きでも：強さがしきい値を超えたら 1 回。半分まで弱まったら次を数えられる
    // どの向きでも：振る勢いがしきい値を超えたら 1 回。勢いが弱まって切り返したら、次を数えられる
    //   GYRO  ：回転の速さ。切り返すときに 0 近くまで落ちるので、往復 1 回で 2 回数える
    //   ACCEL ：加速度の大きさ（重力の 1G をのぞく）
    //   どちらも大きさだけを見るので、Joy-Con の向きが変わっても数えもれしにくい
    void DetectAnyDirection(Vector3 accel, Vector3 gyro)
    {
        float gyroLevel = gyro.magnitude / Mathf.Max(0.01f, m_gyroThreshold);
        float accelLevel = Mathf.Max(0f, accel.magnitude - 1f) / Mathf.Max(0.01f, m_swingThreshold);

        // 確認用の表示（最近の最大）
        m_debugGyroPeak = Mathf.Max(gyro.magnitude, m_debugGyroPeak - Time.deltaTime * 10f);
        m_debugAccelPeak = Mathf.Max(accel.magnitude - 1f, m_debugAccelPeak - Time.deltaTime * 2f);

        float level;

        switch (m_anySource)
        {
            case ANY_SOURCE.ACCEL:
                level = accelLevel;
                break;

            case ANY_SOURCE.GYRO_OR_ACCEL:
                level = Mathf.Max(gyroLevel, accelLevel);
                break;

            default:
                level = gyroLevel;
                break;
        }

        // 今の振りの向き（GYRO のときは回転の向き、ACCEL のときは加速度の向き）
        Vector3 direction = (m_anySource == ANY_SOURCE.ACCEL || (m_anySource == ANY_SOURCE.GYRO_OR_ACCEL && accelLevel > gyroLevel))
            ? accel.normalized
            : gyro.normalized;

        if (level > 1f)
        {
            // 速くブンブン振ると、切り返しで勢いが落ちきらないうちに次の振りが始まる
            // → 勢いが弱まったときだけでなく「向きが逆になったとき」も、次の 1 回として数える
            bool isReversed = Vector3.Dot(direction, m_lastSwingDirection) < -0.2f;

            if ((m_isReleased || isReversed) &&
                Time.time - m_lastSwingTime >= m_minInterval)
            {
                AddSwing(1f);
            }

            m_isReleased = false;
            m_lastSwingDirection = direction;
        }
        else if (level < 0.4f)
        {
            m_isReleased = true;
        }
    }

    // 振った動きのうち、振る向きの成分
    float GetSwing(Vector3 motion)
    {
        if (m_swingAxis == AXIS.GRAVITY)
        {
            return m_gravity.sqrMagnitude > 0.01f
                ? Vector3.Dot(motion, m_gravity.normalized)
                : 0f;
        }

        return GetAxis(motion, m_swingAxis);
    }

    // まっすぐ持てているかを調べる軸
    AXIS GetHoldAxis()
    {
        if (m_swingAxis == AXIS.GRAVITY)
        {
            return m_holdAxis == AXIS.GRAVITY ? AXIS.X : m_holdAxis;
        }

        return m_swingAxis;
    }

    static float GetAxis(Vector3 v, AXIS axis)
    {
        switch (axis)
        {
            case AXIS.Y:
                return v.y;

            case AXIS.Z:
                return v.z;

            default:
                return v.x;
        }
    }

    //==================================================
    // キーボード（W と S を交互に押す。左 Shift でブレ）
    //==================================================
    void UpdateKeyboard()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        bool isBent = keyboard.leftShiftKey.isPressed;
        m_holdStraightness = isBent ? 0.2f : 1f;

        int direction = 0;

        if (keyboard.wKey.wasPressedThisFrame)
        {
            direction = 1;
        }
        else if (keyboard.sKey.wasPressedThisFrame)
        {
            direction = -1;
        }

        // 向きを問わないときは、同じキーの連打でも数える
        if (direction != 0 &&
            (!m_useDirection || direction != m_keyboardDirection))
        {
            m_keyboardDirection = direction;
            AddSwing(m_holdStraightness);
        }

        if (Time.time - m_lastSwingTime > 0.5f)
        {
            m_straightness = Mathf.MoveTowards(m_straightness, m_holdStraightness, Time.deltaTime * 2f);
        }
    }

    //==================================================
    // 1 回振った
    //==================================================
    void AddSwing(float straightness)
    {
        m_lastSwingTime = Time.time;
        m_swingCount++;
        m_swingTimes.Enqueue(Time.time);

        m_straightness = m_useStraightness ? Mathf.Clamp01(straightness) : 1f;

        float point = IsStraight ? m_straightPoint : m_bentPoint;

        m_points += point;
        m_totalPoints += point;

        if (Swung != null)
        {
            Swung(point);
        }
    }

    // 速さの段階を決める
    void UpdateLevel()
    {
        float window = Mathf.Max(0.1f, m_rateWindow);

        while (m_swingTimes.Count > 0 &&
               Time.time - m_swingTimes.Peek() > window)
        {
            m_swingTimes.Dequeue();
        }

        float rate = m_swingTimes.Count / window;

        // 急に変わりすぎないように、少しなめらかにする
        m_swingRate = Mathf.Lerp(m_swingRate, rate, 1f - Mathf.Exp(-Time.deltaTime * 8f));

        if (m_swingRate >= m_fastRate)
        {
            m_level = SPEED_LEVEL.FAST;
        }
        else if (m_swingRate >= m_normalRate)
        {
            m_level = SPEED_LEVEL.NORMAL;
        }
        else if (m_swingRate >= m_slowRate)
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
        m_swingThreshold = Mathf.Max(0.05f, m_swingThreshold);
        m_minInterval = Mathf.Max(0f, m_minInterval);
        m_bentAngle = Mathf.Max(m_straightAngle + 1f, m_bentAngle);
        m_normalRate = Mathf.Max(m_slowRate, m_normalRate);
        m_fastRate = Mathf.Max(m_normalRate, m_fastRate);
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
            m_debugStyle.wordWrap = true;
        }

        string text =
            "【SHAKE_INPUT】\n" +
            "入力：" + (m_joycon != null ? "Joy-Con" : "キーボード（W/S、Shift でブレ）") + "\n" +
            "速さ：" + m_level + "（" + m_swingRate.ToString("0.0") + " 回/秒）\n" +
            "まっすぐさ：" + m_straightness.ToString("0.00") + (IsStraight ? "（まっすぐ）" : "（ブレ）") + "\n" +
            "回数：" + m_swingCount + "　点数：" + m_totalPoints.ToString("0.0") + "\n" +
            "重力：" + m_gravity.ToString("0.00") + "\n" +
            (m_useDirection ? "" :
                "勢い（最近の最大）：回転 " + m_debugGyroPeak.ToString("0.0") + " / しきい値 " + m_gyroThreshold.ToString("0.0") +
                "　加速度 " + m_debugAccelPeak.ToString("0.00") + "G / しきい値 " + m_swingThreshold.ToString("0.00") + "G\n") +
            "（まっすぐ持ったときに一番大きい軸を「まっすぐの軸」にする）";

        const float WIDTH = 620f;
        const float HEIGHT = 300f;
        const float MARGIN = 20f;

        float x = (m_debugPosition == DEBUG_POSITION.LEFT_MIDDLE ||
                   m_debugPosition == DEBUG_POSITION.LEFT_BOTTOM)
                  ? MARGIN
                  : Screen.width - WIDTH - MARGIN;

        float y = (m_debugPosition == DEBUG_POSITION.LEFT_BOTTOM ||
                   m_debugPosition == DEBUG_POSITION.RIGHT_BOTTOM)
                  ? Screen.height - HEIGHT - MARGIN
                  : (Screen.height - HEIGHT) * 0.5f;

        Rect area = new Rect(x, y, WIDTH, HEIGHT);

        // 後ろを暗くして読みやすくする
        Color before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = before;

        GUI.Label(new Rect(x + 12f, y + 8f, WIDTH - 24f, HEIGHT - 16f), text, m_debugStyle);
    }
}
