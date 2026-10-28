using System;
using UnityEngine;
using UnityEngine.Events;

//==================================================
// ゲーム開始時の演出（カウントダウンの前に流れる）
//
//   1. 見下ろし : フィールドを上空から見下ろし、ゆっくり回りながら引いていく
//   2. 寄り     : 自分のキャラのカメラ位置まで滑らかに近づく
//   3. 終了     : プレイヤーのカメラに切り替え → GAME_MANAGER のカウントダウンへ
//
// ・フィールドの中心あたりに空の GameObject を置いて、これを付ける
// ・演出用のカメラは実行時に自動で作る（プレイヤーのカメラの設定をコピー）
// ・プレイヤーのカメラ（澤田さんの Cinemachine）には一切触らない。
//   演出用カメラを手前に重ねて、最後にそのカメラを消すだけ
// ・Enter / Space / ゲームパッドの Start・A でスキップできる
//
// 他のスクリプトからは
//   GAME_INTRO.IsPlaying … 演出中か（GAME_MANAGER はこれが終わるまで待つ）
//   GAME_INTRO.Finished  … 演出が終わった通知
// を使える
//==================================================
[DefaultExecutionOrder(10000)] // カメラが動いた後（Cinemachine の後）に位置を決める
public class GAME_INTRO : MonoBehaviour
{
    enum PHASE
    {
        WAIT,       // シーン遷移が終わるのを待つ
        OVERVIEW,   // 見下ろし（引き）
        ZOOM_IN,    // 自分のキャラへ寄る
        FINISHED
    }

    public static bool IsPlaying { get; private set; }

    public static event Action Finished;

    [Header("引き継ぐカメラ")]
    [Tooltip("演出のあとに使うプレイヤーのカメラ。空欄なら MainCamera タグのカメラを使う")]
    [SerializeField]
    private Camera m_playerCamera;

    [Header("1. 見下ろし（引き）")]
    [Tooltip("見下ろす中心。空欄ならこのオブジェクトの位置")]
    [SerializeField]
    private Transform m_center;

    [Tooltip("見下ろしの時間（秒）。仕様は 3～5 秒")]
    [SerializeField]
    private float m_overviewTime = 4f;

    [Tooltip("見下ろす角度。90 で真上から")]
    [Range(10f, 90f)]
    [SerializeField]
    private float m_pitch = 60f;

    [Tooltip("中心からカメラまでの距離（開始時）")]
    [SerializeField]
    private float m_startDistance = 45f;

    [Tooltip("中心からカメラまでの距離（終了時）。開始より大きいと引いていく")]
    [SerializeField]
    private float m_endDistance = 90f;

    [Tooltip("見下ろし中にぐるっと回る角度。0 で回らない")]
    [SerializeField]
    private float m_spinAngle = 40f;

    [Header("2. 寄り")]
    [Tooltip("自分のキャラに寄るまでの時間（秒）")]
    [SerializeField]
    private float m_zoomInTime = 2f;

    [Header("その他")]
    [Tooltip("シーン遷移（Liu さんのトランジション）が終わってから始める")]
    [SerializeField]
    private bool m_waitForTransition = true;

    [Tooltip("決定ボタンでスキップできる")]
    [SerializeField]
    private bool m_allowSkip = true;

    [Tooltip("演出が終わったときに呼ばれる")]
    [SerializeField]
    private UnityEvent m_onFinished = new UnityEvent();

    private PHASE m_phase = PHASE.FINISHED;
    private float m_timer;

    private Camera m_introCamera;

    // 寄りを始めた時点のカメラの状態
    private Vector3 m_zoomFromPosition;
    private Quaternion m_zoomFromRotation;
    private float m_zoomFromFov;

    // ドメインリロード無しで再生した場合に前回の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsPlaying = false;
        Finished = null;
    }

    void Awake()
    {
        // GAME_MANAGER の Start より前に「演出中」にしておく
        // （チェックを外している場合は演出しない）
        if (enabled)
        {
            IsPlaying = true;
            m_phase = PHASE.WAIT;
        }
    }

    void Start()
    {
        if (m_playerCamera == null)
        {
            m_playerCamera = Camera.main;
        }

        if (m_playerCamera == null)
        {
            Debug.LogWarning("GAME_INTRO：プレイヤーのカメラが見つからないので演出を飛ばします");
            Finish();
            return;
        }

        CreateIntroCamera();

        // 遷移中も、裏では最初の見下ろし位置にしておく
        ApplyOverview(0f);
    }

    void OnDisable()
    {
        // 途中でオブジェクトが消えたり無効になったりしても、ゲームが止まらないようにする
        if (m_phase != PHASE.FINISHED)
        {
            Finish();
        }
    }

    void LateUpdate()
    {
        switch (m_phase)
        {
            case PHASE.WAIT:
                UpdateWait();
                break;

            case PHASE.OVERVIEW:
                UpdateOverview();
                break;

            case PHASE.ZOOM_IN:
                UpdateZoomIn();
                break;
        }
    }

    //==================================================
    // 各段階
    //==================================================

    void UpdateWait()
    {
        ApplyOverview(0f);

        if (m_waitForTransition &&
            GAME_SCENE_LOADER.IsTransitioning)
        {
            return;
        }

        m_phase = PHASE.OVERVIEW;
        m_timer = 0f;
    }

    void UpdateOverview()
    {
        m_timer += Time.deltaTime;

        float t =
            Mathf.Clamp01(m_timer / Mathf.Max(m_overviewTime, 0.01f));

        ApplyOverview(t);

        if (t >= 1f ||
            IsSkipPressed())
        {
            BeginZoomIn();
        }
    }

    void BeginZoomIn()
    {
        m_phase = PHASE.ZOOM_IN;
        m_timer = 0f;

        m_zoomFromPosition = m_introCamera.transform.position;
        m_zoomFromRotation = m_introCamera.transform.rotation;
        m_zoomFromFov = m_introCamera.fieldOfView;
    }

    void UpdateZoomIn()
    {
        m_timer += Time.deltaTime;

        float t =
            Mathf.Clamp01(m_timer / Mathf.Max(m_zoomInTime, 0.01f));

        // 動き始めと止まる直前をゆっくりにする
        float eased =
            Mathf.SmoothStep(0f, 1f, t);

        // 行き先はプレイヤーのカメラ（毎フレーム最新の位置を見る）
        Transform target = m_playerCamera.transform;

        m_introCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(m_zoomFromPosition, target.position, eased),
            Quaternion.Slerp(m_zoomFromRotation, target.rotation, eased));

        m_introCamera.fieldOfView =
            Mathf.Lerp(m_zoomFromFov, m_playerCamera.fieldOfView, eased);

        if (t >= 1f ||
            IsSkipPressed())
        {
            Finish();
        }
    }

    void Finish()
    {
        if (m_phase == PHASE.FINISHED &&
            !IsPlaying)
        {
            return;
        }

        m_phase = PHASE.FINISHED;
        IsPlaying = false;

        // 演出用カメラを消すと、プレイヤーのカメラの映像に切り替わる
        if (m_introCamera != null)
        {
            Destroy(m_introCamera.gameObject);
            m_introCamera = null;
        }

        m_onFinished.Invoke();

        if (Finished != null)
        {
            Finished();
        }
    }

    //==================================================
    // カメラ
    //==================================================

    void CreateIntroCamera()
    {
        GameObject cameraObject =
            new GameObject("IntroCamera（自動作成）");

        m_introCamera = cameraObject.AddComponent<Camera>();

        // 描画の設定（背景・描画距離・出力先のディスプレイなど）をプレイヤーのカメラに合わせる
        m_introCamera.CopyFrom(m_playerCamera);

        // プレイヤーのカメラより手前に描く（上に重ねて隠す）
        m_introCamera.depth = m_playerCamera.depth + 10f;
    }

    // 見下ろしのカメラ位置（t = 0 開始 ～ 1 終了）
    void ApplyOverview(float t)
    {
        if (m_introCamera == null)
        {
            return;
        }

        // 引きは最初に速く、だんだんゆっくりにする
        float eased =
            1f - (1f - t) * (1f - t);

        Vector3 position;
        Quaternion rotation;

        GetOverviewPose(eased, GetPlayerYaw(), out position, out rotation);

        m_introCamera.transform.SetPositionAndRotation(position, rotation);
        m_introCamera.fieldOfView = m_playerCamera.fieldOfView;
    }

    // 見下ろしの終わりに、プレイヤーのカメラと同じ向きになるように回す
    // （そのほうが寄るときに自然に見える）
    void GetOverviewPose(float eased, float playerYaw, out Vector3 position, out Quaternion rotation)
    {
        float distance =
            Mathf.Lerp(m_startDistance, m_endDistance, eased);

        float yaw =
            playerYaw - m_spinAngle * (1f - eased);

        rotation =
            Quaternion.Euler(m_pitch, yaw, 0f);

        position =
            GetCenter() - rotation * Vector3.forward * distance;
    }

    float GetPlayerYaw()
    {
        return m_playerCamera != null
            ? m_playerCamera.transform.eulerAngles.y
            : 0f;
    }

    Vector3 GetCenter()
    {
        return m_center != null
            ? m_center.position
            : transform.position;
    }

    bool IsSkipPressed()
    {
        return m_allowSkip &&
               GAME_INPUT.WasSubmitPressed();
    }

    //==================================================
    // エディター
    //==================================================

    void OnValidate()
    {
        m_overviewTime = Mathf.Max(0f, m_overviewTime);
        m_zoomInTime = Mathf.Max(0f, m_zoomInTime);
        m_startDistance = Mathf.Max(1f, m_startDistance);
        m_endDistance = Mathf.Max(1f, m_endDistance);
    }

    // 選択中に、見下ろしカメラの開始位置（水色）と終了位置（青）を表示する
    void OnDrawGizmosSelected()
    {
        Vector3 center = GetCenter();

        float yaw = GetPlayerYaw();

        Vector3 startPosition;
        Vector3 endPosition;
        Quaternion rotation;

        GetOverviewPose(0f, yaw, out startPosition, out rotation);
        GetOverviewPose(1f, yaw, out endPosition, out rotation);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 1f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(startPosition, 2f);
        Gizmos.DrawLine(startPosition, center);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(endPosition, 2f);
        Gizmos.DrawLine(endPosition, center);
    }
}
