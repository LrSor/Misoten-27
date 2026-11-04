using System.Collections.Generic;
using UnityEngine;

//==================================================
// 音を鳴らす係（ゲームに 1 つだけ。シーンを切り替えても残る）
//
// ■ シーンに置かなくてよい
//   初めて音を鳴らしたときに自動で作られる。
//   音量は Resources/SOUND_SETTINGS から読み込む
//
// ■ 使い方（どのスクリプトからでも呼べる）
//   SOUND_MANAGER.Play(sound);                  … 鳴らす（立体音の設定なら、この係の位置＝音が遠くなるので注意）
//   SOUND_MANAGER.Play(sound, position);        … その場所で鳴らす
//   SOUND_MANAGER.Play(sound, transform);       … そのオブジェクトについていきながら鳴らす
//   SOUND_HANDLE h = SOUND_MANAGER.Play(...);   … くり返しの音を後で止めたいときは h.Stop();
//
//   SOUND_MANAGER.PlayBGM(bgm);                 … BGM を切り替える（前の BGM はフェードアウト）
//   SOUND_MANAGER.StopBGM();                    … BGM を止める
//
//   種類が BGM のサウンドデータを Play に渡した場合も、自動で PlayBGM になる
//
// ■ 音を聞く「耳」（AudioListener）について
//   耳がシーンにないと、何も聞こえない（澤田さんの Player のカメラの耳はオフになっている）。
//   そこで、有効な耳がシーンに 1 つもないときは、この係が自分の耳をオンにして、
//   今映っているカメラ（Camera.main など）の位置についていく。
//   ほかに有効な耳があるときは自分の耳をオフにする（耳が 2 つあると警告が出るため）。
//   画面分割で 4 人になったときは、耳をどこに置くか決める必要がある
//==================================================
[DefaultExecutionOrder(-500)]
public class SOUND_MANAGER : MonoBehaviour
{
    const string SETTINGS_PATH = "SOUND_SETTINGS";

    // 鳴らしている音 1 つ分
    private class VOICE
    {
        public AudioSource m_source;
        public SOUND_DATA m_data;
        public float m_baseVolume;
        public Transform m_follow;
        public int m_id;
        public float m_startTime;
    }

    // BGM 1 つ分（フェード用）
    private class BGM_TRACK
    {
        public AudioSource m_source;
        public SOUND_DATA m_data;
        public float m_baseVolume;
        public float m_fade;        // 0～1
        public float m_fadeTarget;  // 0 か 1
        public float m_fadeSpeed;   // 1 秒あたりの変化量
    }

    private static SOUND_MANAGER s_instance;
    private static bool s_isQuitting;

    private SOUND_SETTINGS m_settings;

    private readonly List<VOICE> m_voices = new List<VOICE>();
    private readonly Dictionary<SOUND_DATA, float> m_lastPlayTimes = new Dictionary<SOUND_DATA, float>();

    private BGM_TRACK m_bgmCurrent;
    private BGM_TRACK m_bgmPrevious;

    private int m_nextId = 1;

    // シーンに耳がないときに使う、この係の耳
    private AudioListener m_listener;
    private int m_listenerCheckFrame;

    // 耳を探す間隔（フレーム）
    const int LISTENER_CHECK_INTERVAL = 10;

    // 音がないデータの警告を 1 回だけ出すため
    private readonly HashSet<SOUND_DATA> m_warned = new HashSet<SOUND_DATA>();

    //==================================================
    // 外から使う関数
    //==================================================

    public static SOUND_HANDLE Play(SOUND_DATA sound)
    {
        SOUND_MANAGER manager = GetInstance();
        return manager != null ? manager.PlayInternal(sound, null, null) : SOUND_HANDLE.NONE;
    }

    public static SOUND_HANDLE Play(SOUND_DATA sound, Vector3 position)
    {
        SOUND_MANAGER manager = GetInstance();
        return manager != null ? manager.PlayInternal(sound, position, null) : SOUND_HANDLE.NONE;
    }

    public static SOUND_HANDLE Play(SOUND_DATA sound, Transform follow)
    {
        SOUND_MANAGER manager = GetInstance();

        if (manager == null)
        {
            return SOUND_HANDLE.NONE;
        }

        Vector3? position = follow != null ? follow.position : (Vector3?)null;

        return manager.PlayInternal(sound, position, follow);
    }

    // fadeTime を省略すると SOUND_SETTINGS のフェード時間を使う
    public static void PlayBGM(SOUND_DATA bgm, float fadeTime = -1f)
    {
        SOUND_MANAGER manager = GetInstance();

        if (manager != null)
        {
            manager.PlayBGMInternal(bgm, fadeTime);
        }
    }

    public static void StopBGM(float fadeTime = -1f)
    {
        // 止めるだけのために係を作らない
        if (s_instance != null)
        {
            s_instance.StopBGMInternal(fadeTime);
        }
    }

    // 今流れている BGM（なければ null）
    public static SOUND_DATA CurrentBGM
    {
        get
        {
            return s_instance != null && s_instance.m_bgmCurrent != null
                ? s_instance.m_bgmCurrent.m_data
                : null;
        }
    }

    // BGM 以外の音を全部止める
    public static void StopAll()
    {
        if (s_instance == null)
        {
            return;
        }

        foreach (VOICE voice in s_instance.m_voices)
        {
            s_instance.Release(voice);
        }
    }

    public static void Stop(SOUND_HANDLE handle)
    {
        if (s_instance == null ||
            !handle.IsValid)
        {
            return;
        }

        VOICE voice = s_instance.FindVoice(handle);

        if (voice != null)
        {
            s_instance.Release(voice);
        }
    }

    public static bool IsPlaying(SOUND_HANDLE handle)
    {
        return s_instance != null &&
               handle.IsValid &&
               s_instance.FindVoice(handle) != null;
    }

    //==================================================
    // 係の作成
    //==================================================

    // ドメインリロード無しで再生した場合に前回の状態が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_instance = null;
        s_isQuitting = false;
    }

    static SOUND_MANAGER GetInstance()
    {
        if (s_instance != null)
        {
            return s_instance;
        }

        // ゲーム終了中や、再生していないときは作らない
        if (s_isQuitting ||
            !Application.isPlaying)
        {
            return null;
        }

        GameObject managerObject = new GameObject("SOUND_MANAGER（自動作成）");
        managerObject.AddComponent<SOUND_MANAGER>();

        return s_instance;
    }

    void Awake()
    {
        if (s_instance != null &&
            s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        m_settings = Resources.Load<SOUND_SETTINGS>(SETTINGS_PATH);

        if (m_settings == null)
        {
            Debug.LogWarning(
                "SOUND_MANAGER：Resources に SOUND_SETTINGS がないので、標準の音量で鳴らします");

            m_settings = ScriptableObject.CreateInstance<SOUND_SETTINGS>();
        }

        m_bgmCurrent = CreateBgmTrack("BGM_A");
        m_bgmPrevious = CreateBgmTrack("BGM_B");

        CreateListener();
    }

    void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    //==================================================
    // 効果音など（BGM 以外）
    //==================================================

    SOUND_HANDLE PlayInternal(SOUND_DATA sound, Vector3? position, Transform follow)
    {
        if (sound == null)
        {
            return SOUND_HANDLE.NONE;
        }

        if (sound.Category == SOUND_DATA.CATEGORY.BGM)
        {
            PlayBGMInternal(sound, -1f);
            return SOUND_HANDLE.NONE;
        }

        if (!CheckClip(sound))
        {
            return SOUND_HANDLE.NONE;
        }

        // 鳴らしすぎ防止（短い間隔での連続）
        float now = Time.unscaledTime;
        float lastTime;

        if (sound.MinInterval > 0f &&
            m_lastPlayTimes.TryGetValue(sound, out lastTime) &&
            now - lastTime < sound.MinInterval)
        {
            return SOUND_HANDLE.NONE;
        }

        // 鳴らしすぎ防止（同時に鳴らす数）
        if (sound.MaxCount > 0 &&
            CountPlaying(sound) >= sound.MaxCount)
        {
            return SOUND_HANDLE.NONE;
        }

        VOICE voice = GetFreeVoice();

        voice.m_data = sound;
        voice.m_follow = follow;
        voice.m_baseVolume = sound.PickVolume();
        voice.m_id = m_nextId++;
        voice.m_startTime = now;

        AudioSource source = voice.m_source;

        source.clip = sound.PickClip();
        source.loop = sound.IsLoop;
        source.pitch = sound.PickPitch();
        source.volume = voice.m_baseVolume * m_settings.GetVolume(sound.Category);

        if (sound.Is3D)
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = sound.MinDistance;
            source.maxDistance = sound.MaxDistance;
            source.transform.position = position.HasValue ? position.Value : transform.position;
        }
        else
        {
            // どこで鳴っても同じ大きさ
            source.spatialBlend = 0f;
            source.transform.localPosition = Vector3.zero;
        }

        source.Play();

        m_lastPlayTimes[sound] = now;

        return new SOUND_HANDLE(voice.m_id);
    }

    void Update()
    {
        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data == null)
            {
                continue;
            }

            // 鳴り終わったら空ける
            if (!voice.m_source.isPlaying)
            {
                Release(voice);
                continue;
            }

            // Inspector で音量を変えたときにすぐ反映する
            voice.m_source.volume =
                voice.m_baseVolume * m_settings.GetVolume(voice.m_data.Category);
        }

        UpdateBgmTrack(m_bgmCurrent);
        UpdateBgmTrack(m_bgmPrevious);
    }

    // ついていく音の位置を、オブジェクトが動いた後に合わせる
    void LateUpdate()
    {
        UpdateListener();

        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data == null ||
                voice.m_follow == null ||
                !voice.m_data.Is3D)
            {
                continue;
            }

            voice.m_source.transform.position = voice.m_follow.position;
        }
    }

    VOICE GetFreeVoice()
    {
        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data == null)
            {
                return voice;
            }
        }

        if (m_voices.Count < m_settings.MaxSources)
        {
            VOICE newVoice = new VOICE();

            GameObject sourceObject = new GameObject("SE_" + m_voices.Count);
            sourceObject.transform.SetParent(transform, false);

            newVoice.m_source = sourceObject.AddComponent<AudioSource>();
            newVoice.m_source.playOnAwake = false;

            m_voices.Add(newVoice);

            return newVoice;
        }

        // いっぱいのときは、くり返しでない一番古い音を止めて使う
        VOICE oldest = null;

        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data != null &&
                voice.m_data.IsLoop)
            {
                continue;
            }

            if (oldest == null ||
                voice.m_startTime < oldest.m_startTime)
            {
                oldest = voice;
            }
        }

        if (oldest == null)
        {
            oldest = m_voices[0];
        }

        Release(oldest);

        return oldest;
    }

    void Release(VOICE voice)
    {
        voice.m_source.Stop();
        voice.m_source.clip = null;
        voice.m_data = null;
        voice.m_follow = null;
        voice.m_id = 0;
    }

    VOICE FindVoice(SOUND_HANDLE handle)
    {
        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data != null &&
                voice.m_id == handle.Id)
            {
                return voice;
            }
        }

        return null;
    }

    int CountPlaying(SOUND_DATA sound)
    {
        int count = 0;

        foreach (VOICE voice in m_voices)
        {
            if (voice.m_data == sound)
            {
                count++;
            }
        }

        return count;
    }

    bool CheckClip(SOUND_DATA sound)
    {
        if (sound.HasClip)
        {
            return true;
        }

        if (m_warned.Add(sound))
        {
            Debug.LogWarning(
                "SOUND_MANAGER：サウンドデータ「" + sound.name + "」に音のファイルが入っていません", sound);
        }

        return false;
    }

    //==================================================
    // 耳（AudioListener）
    //==================================================

    void CreateListener()
    {
        GameObject listenerObject = new GameObject("Listener");
        listenerObject.transform.SetParent(transform, false);

        m_listener = listenerObject.AddComponent<AudioListener>();
        m_listener.enabled = false;

        // 最初のフレームですぐに確認する
        m_listenerCheckFrame = 0;
    }

    void UpdateListener()
    {
        if (m_listener == null)
        {
            return;
        }

        // ほかの耳があるかの確認は、少し間をあけて行う（毎フレーム探すと重いため）
        if (Time.frameCount >= m_listenerCheckFrame)
        {
            m_listenerCheckFrame = Time.frameCount + LISTENER_CHECK_INTERVAL;

            bool hasOtherListener = false;

            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener != m_listener &&
                    listener.isActiveAndEnabled)
                {
                    hasOtherListener = true;
                    break;
                }
            }

            if (m_listener.enabled == hasOtherListener)
            {
                m_listener.enabled = !hasOtherListener;
            }
        }

        // 自分の耳を使っているときは、今映っているカメラの位置で聞く（立体音のため）
        if (m_listener.enabled)
        {
            Camera camera = GetListeningCamera();

            if (camera != null)
            {
                m_listener.transform.SetPositionAndRotation(
                    camera.transform.position,
                    camera.transform.rotation);
            }
        }
    }

    // MainCamera タグのカメラ。なければ、映っているカメラの中で一番手前に描かれるもの
    static Camera GetListeningCamera()
    {
        Camera main = Camera.main;

        if (main != null)
        {
            return main;
        }

        Camera best = null;

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera.targetTexture != null)
            {
                continue;
            }

            if (best == null ||
                camera.depth > best.depth)
            {
                best = camera;
            }
        }

        return best;
    }

    //==================================================
    // BGM（2 つの再生機を交互に使ってクロスフェードする）
    //==================================================

    BGM_TRACK CreateBgmTrack(string name)
    {
        GameObject sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(transform, false);

        BGM_TRACK track = new BGM_TRACK();

        track.m_source = sourceObject.AddComponent<AudioSource>();
        track.m_source.playOnAwake = false;
        track.m_source.spatialBlend = 0f;

        return track;
    }

    void PlayBGMInternal(SOUND_DATA bgm, float fadeTime)
    {
        if (bgm == null)
        {
            StopBGMInternal(fadeTime);
            return;
        }

        if (!CheckClip(bgm))
        {
            return;
        }

        if (fadeTime < 0f)
        {
            fadeTime = m_settings.BgmFadeTime;
        }

        // 同じ BGM が流れていたら、そのまま続ける（シーンをまたいでも途切れない）
        if (m_bgmCurrent.m_data == bgm &&
            m_bgmCurrent.m_source.isPlaying &&
            m_bgmCurrent.m_fadeTarget > 0f)
        {
            return;
        }

        // 今の BGM を「前の BGM」にしてフェードアウト
        BGM_TRACK previous = m_bgmCurrent;
        m_bgmCurrent = m_bgmPrevious;
        m_bgmPrevious = previous;

        StartFade(m_bgmPrevious, 0f, fadeTime);

        // 新しい BGM をフェードイン
        BGM_TRACK track = m_bgmCurrent;

        track.m_data = bgm;
        track.m_baseVolume = bgm.PickVolume();
        track.m_source.clip = bgm.PickClip();
        track.m_source.loop = bgm.IsLoop;
        track.m_source.pitch = bgm.PickPitch();
        track.m_fade = fadeTime > 0f ? 0f : 1f;

        StartFade(track, 1f, fadeTime);

        ApplyBgmVolume(track);
        track.m_source.Play();
    }

    void StopBGMInternal(float fadeTime)
    {
        if (fadeTime < 0f)
        {
            fadeTime = m_settings.BgmFadeTime;
        }

        StartFade(m_bgmCurrent, 0f, fadeTime);
        StartFade(m_bgmPrevious, 0f, fadeTime);
    }

    void StartFade(BGM_TRACK track, float target, float fadeTime)
    {
        track.m_fadeTarget = target;

        if (fadeTime <= 0f)
        {
            track.m_fade = target;
            track.m_fadeSpeed = 0f;
        }
        else
        {
            track.m_fadeSpeed = 1f / fadeTime;
        }
    }

    void UpdateBgmTrack(BGM_TRACK track)
    {
        if (track.m_data == null)
        {
            return;
        }

        // シーン遷移中に止まらないよう、ゲーム内の時間ではなく実際の時間で進める
        track.m_fade =
            Mathf.MoveTowards(track.m_fade, track.m_fadeTarget, track.m_fadeSpeed * Time.unscaledDeltaTime);

        ApplyBgmVolume(track);

        // フェードアウトし終わった、または 1 回だけの BGM が鳴り終わった
        if ((track.m_fadeTarget <= 0f && track.m_fade <= 0f) ||
            !track.m_source.isPlaying)
        {
            track.m_source.Stop();
            track.m_source.clip = null;
            track.m_data = null;
        }
    }

    void ApplyBgmVolume(BGM_TRACK track)
    {
        if (track.m_data == null)
        {
            return;
        }

        track.m_source.volume =
            track.m_baseVolume * track.m_fade * m_settings.GetVolume(SOUND_DATA.CATEGORY.BGM);
    }
}

//==================================================
// 鳴らした音の「引換券」。くり返しの音を後で止めるときに使う
//   SOUND_HANDLE h = SOUND_MANAGER.Play(engine, transform);
//   h.Stop();
//==================================================
public struct SOUND_HANDLE
{
    public static readonly SOUND_HANDLE NONE = new SOUND_HANDLE(0);

    private readonly int m_id;

    public SOUND_HANDLE(int id)
    {
        m_id = id;
    }

    public int Id { get { return m_id; } }

    public bool IsValid { get { return m_id != 0; } }

    public bool IsPlaying { get { return SOUND_MANAGER.IsPlaying(this); } }

    public void Stop()
    {
        SOUND_MANAGER.Stop(this);
    }
}
