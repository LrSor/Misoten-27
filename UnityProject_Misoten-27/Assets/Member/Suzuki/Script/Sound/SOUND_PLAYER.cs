using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// コードを書かずに音を鳴らすための部品
//
// オブジェクトに付けて、サウンドデータと「いつ鳴らすか」を選ぶだけ
//   START      : シーンが始まったとき（シーンの BGM を決めるときなど）
//   ENABLE     : 表示されたとき（オブジェクトがオンになるたび）
//   COLLISION  : 何かがぶつかったとき
//   TRIGGER    : トリガーの範囲に何かが入ったとき
//   NONE       : 自動では鳴らさない（ボタンの OnClick や UnityEvent から Play を呼ぶ）
//
// ・START のときは、シーン遷移が終わるのを待ってから鳴らす
//   （遷移中は BGM が流れないようにするため。「遷移が終わってから鳴らす」で切り替えられる）
// ・「すること」を「BGM を止める」にすると、音を鳴らす代わりに今の BGM をフェードアウトする
//   （BGM がないシーンに置くと、前のシーンの BGM を止められる）
// ・サウンドデータの種類が BGM なら、BGM の切り替えになる
// ・サウンドデータが立体音なら、このオブジェクトの位置で鳴り、動くとついていく
// ・Inspector の右上の︙（または右クリック）→「テスト再生」で、再生中に鳴らして確認できる
// ・「テスト用のキー」を選ぶと、再生中にそのキーで鳴らせる（エディターと開発ビルドのみ）
//==================================================
public class SOUND_PLAYER : MonoBehaviour
{
    public enum TIMING
    {
        NONE,
        START,
        ENABLE,
        COLLISION,
        TRIGGER,
    }

    public enum ACTION
    {
        PLAY,       // 音を鳴らす
        STOP_BGM,   // 今流れている BGM を止める（サウンドデータは空欄でよい）
    }

    [Tooltip("すること")]
    [SerializeField]
    private ACTION m_action = ACTION.PLAY;

    [Tooltip("鳴らす音")]
    [SerializeField]
    private SOUND_DATA m_sound;

    [Tooltip("いつ鳴らすか")]
    [SerializeField]
    private TIMING m_timing = TIMING.START;

    [Tooltip("START のとき、シーン遷移が終わるのを待ってから鳴らす")]
    [SerializeField]
    private bool m_waitForTransition = true;

    [Tooltip("COLLISION / TRIGGER のとき、このタグのものだけに反応する（空欄なら何にでも反応）")]
    [SerializeField]
    private string m_targetTag = "Player";

    [Tooltip("このオブジェクトが消えたり非表示になったりしたら、鳴らしている音を止める（くり返しの音向け。BGM は止めない）")]
    [SerializeField]
    private bool m_stopOnDisable = true;

    [Tooltip("テスト用：再生中にこのキーを押すと鳴らす（None で使わない。エディターと開発ビルドのみ）")]
    [SerializeField]
    private Key m_testKey = Key.None;

    private SOUND_HANDLE m_handle;

    // シーン遷移が終わるのを待っている
    private bool m_isWaitingStart;

    public SOUND_DATA Sound
    {
        get { return m_sound; }
        set { m_sound = value; }
    }

    void Start()
    {
        if (m_timing != TIMING.START)
        {
            return;
        }

        if (m_waitForTransition &&
            GAME_SCENE_LOADER.IsTransitioning)
        {
            m_isWaitingStart = true;
            return;
        }

        Play();
    }

    void OnEnable()
    {
        if (m_timing == TIMING.ENABLE)
        {
            Play();
        }
    }

    void OnDisable()
    {
        if (m_stopOnDisable)
        {
            m_handle.Stop();
        }
    }

    void Update()
    {
        // シーン遷移が終わったら鳴らす
        if (m_isWaitingStart &&
            !GAME_SCENE_LOADER.IsTransitioning)
        {
            m_isWaitingStart = false;
            Play();
        }

        UpdateTestKey();
    }

    void UpdateTestKey()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (m_testKey == Key.None ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[m_testKey].wasPressedThisFrame)
        {
            Debug.Log(
                "SOUND_PLAYER：テスト用のキー［" + m_testKey + "］で「" +
                (m_sound != null ? m_sound.name : "（サウンドデータなし）") + "」を鳴らします", this);

            Play();
        }
#endif
    }

    void OnCollisionEnter(Collision collision)
    {
        if (m_timing == TIMING.COLLISION &&
            IsTarget(collision.gameObject))
        {
            Play();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (m_timing == TIMING.TRIGGER &&
            IsTarget(other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject))
        {
            Play();
        }
    }

    bool IsTarget(GameObject target)
    {
        return string.IsNullOrEmpty(m_targetTag) ||
               target.CompareTag(m_targetTag);
    }

    // ボタンの OnClick や UnityEvent からも呼べる
    public void Play()
    {
        if (m_action == ACTION.STOP_BGM)
        {
            SOUND_MANAGER.StopBGM();
            return;
        }

        if (m_sound == null)
        {
            return;
        }

        if (m_sound.Category == SOUND_DATA.CATEGORY.BGM)
        {
            SOUND_MANAGER.PlayBGM(m_sound);
            return;
        }

        m_handle = SOUND_MANAGER.Play(m_sound, transform);
    }

    public void Stop()
    {
        if (m_sound != null &&
            m_sound.Category == SOUND_DATA.CATEGORY.BGM)
        {
            if (SOUND_MANAGER.CurrentBGM == m_sound)
            {
                SOUND_MANAGER.StopBGM();
            }

            return;
        }

        m_handle.Stop();
    }

    [ContextMenu("テスト再生")]
    void TestPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("SOUND_PLAYER：テスト再生は、ゲームを再生しているときに使ってください");
            return;
        }

        Play();
    }

    [ContextMenu("テスト停止")]
    void TestStop()
    {
        if (Application.isPlaying)
        {
            Stop();
        }
    }
}
