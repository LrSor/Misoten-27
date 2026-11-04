using UnityEngine;

//==================================================
// 音 1 つ分の設定（サウンドデータ）
//
// ■ 作り方
//   Project ウィンドウで右クリック →「Create / Misoten / Sound Data」
//   → 音のファイル（AudioClip）を入れて、音量などを決める
//
// ■ 使い方
//   ・コードから：SOUND_DATA を Inspector で受け取って
//       SOUND_MANAGER.Play(m_sound);                 … どこで鳴っても同じ大きさ
//       SOUND_MANAGER.Play(m_sound, transform);      … このオブジェクトの位置で鳴らす（立体音のとき）
//   ・コードを書かずに：オブジェクトに SOUND_PLAYER を付けてこのデータを入れる
//
// ・音のファイルを複数入れると、鳴らすたびにランダムで選ぶ（同じ音ばかりにならない）
// ・音量は「このデータの音量 × 種類ごとの音量 × 全体の音量」で決まる
//   （種類ごと・全体の音量は Resources/SOUND_SETTINGS で調整）
//==================================================
[CreateAssetMenu(fileName = "SE_New", menuName = "Misoten/Sound Data", order = 0)]
public class SOUND_DATA : ScriptableObject
{
    public enum CATEGORY
    {
        BGM,    // 音楽（同時に 1 曲だけ。切り替えるとフェードする）
        SE,     // 効果音
        UI,     // メニュー・ボタンなどの音
        VOICE,  // 声
    }

    public enum LOOP
    {
        AUTO,   // BGM はくり返す、それ以外はくり返さない
        ON,     // くり返す
        OFF,    // くり返さない（BGM でもジングルなど 1 回だけのとき）
    }

    [Header("音のファイル（複数入れるとランダムで選ぶ）")]
    [SerializeField]
    private AudioClip[] m_clips;

    [Header("種類")]
    [SerializeField]
    private CATEGORY m_category = CATEGORY.SE;

    [Header("音量")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_volume = 1f;

    [Tooltip("鳴らすたびに音量を少しだけ下げてばらつかせる幅（0 でばらつかない）")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float m_volumeRandom = 0f;

    [Header("音の高さ")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float m_pitch = 1f;

    [Tooltip("鳴らすたびに音の高さをばらつかせる幅（0.05 くらいで自然になる）")]
    [Range(0f, 0.5f)]
    [SerializeField]
    private float m_pitchRandom = 0f;

    [Header("くり返し")]
    [SerializeField]
    private LOOP m_loop = LOOP.AUTO;

    [Header("立体音（位置によって聞こえ方が変わる）")]
    [Tooltip("オンにすると、鳴った場所が近いほど大きく、遠いほど小さく聞こえる")]
    [SerializeField]
    private bool m_is3D = false;

    [Tooltip("この距離（m）までは一番大きく聞こえる")]
    [SerializeField]
    private float m_minDistance = 2f;

    [Tooltip("この距離（m）より遠いと聞こえなくなる")]
    [SerializeField]
    private float m_maxDistance = 30f;

    [Header("鳴らしすぎ防止")]
    [Tooltip("前に鳴らしてからこの秒数がたつまでは、同じ音を鳴らさない（同じフレームで何個も重なるのを防ぐ）")]
    [SerializeField]
    private float m_minInterval = 0.05f;

    [Tooltip("同時に鳴らせる数（0 で制限なし）")]
    [SerializeField]
    private int m_maxCount = 0;

    public CATEGORY Category { get { return m_category; } }
    public float Volume { get { return m_volume; } }
    public bool Is3D { get { return m_is3D; } }
    public float MinDistance { get { return m_minDistance; } }
    public float MaxDistance { get { return m_maxDistance; } }
    public float MinInterval { get { return m_minInterval; } }
    public int MaxCount { get { return m_maxCount; } }

    public bool IsLoop
    {
        get
        {
            switch (m_loop)
            {
                case LOOP.ON:
                    return true;

                case LOOP.OFF:
                    return false;

                default:
                    return m_category == CATEGORY.BGM;
            }
        }
    }

    public bool HasClip
    {
        get
        {
            if (m_clips == null)
            {
                return false;
            }

            foreach (AudioClip clip in m_clips)
            {
                if (clip != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // 鳴らす音のファイルを選ぶ（空欄は飛ばす）
    public AudioClip PickClip()
    {
        if (!HasClip)
        {
            return null;
        }

        while (true)
        {
            AudioClip clip = m_clips[Random.Range(0, m_clips.Length)];

            if (clip != null)
            {
                return clip;
            }
        }
    }

    // 鳴らすたびのばらつきを入れた音量（種類・全体の音量は掛けていない）
    public float PickVolume()
    {
        return m_volume * (1f - Random.Range(0f, m_volumeRandom));
    }

    public float PickPitch()
    {
        return Mathf.Max(0.1f, m_pitch + Random.Range(-m_pitchRandom, m_pitchRandom));
    }

    void OnValidate()
    {
        m_minDistance = Mathf.Max(0.1f, m_minDistance);
        m_maxDistance = Mathf.Max(m_minDistance + 0.1f, m_maxDistance);
        m_minInterval = Mathf.Max(0f, m_minInterval);
        m_maxCount = Mathf.Max(0, m_maxCount);
    }
}
