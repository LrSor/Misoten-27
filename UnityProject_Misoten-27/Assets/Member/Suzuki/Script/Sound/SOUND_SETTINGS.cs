using UnityEngine;

//==================================================
// ゲーム全体の音量設定
//
// ・Resources フォルダの「SOUND_SETTINGS」を SOUND_MANAGER が自動で読み込む
//   （Assets/Member/Suzuki/Resources/SOUND_SETTINGS）
// ・再生中に Inspector で変えても、すぐに音量に反映される
// ・オプション画面を作るときは、ここの値を変えれば全体に効く
//==================================================
[CreateAssetMenu(fileName = "SOUND_SETTINGS", menuName = "Misoten/Sound Settings", order = 1)]
public class SOUND_SETTINGS : ScriptableObject
{
    [Header("音量（0 で無音、1 で最大）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float m_masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float m_bgmVolume = 0.6f;

    [Range(0f, 1f)]
    [SerializeField]
    private float m_seVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float m_uiVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField]
    private float m_voiceVolume = 1f;

    [Header("BGM")]
    [Tooltip("BGM を切り替えるときのフェードの秒数")]
    [SerializeField]
    private float m_bgmFadeTime = 1f;

    [Header("同時に鳴らせる音")]
    [Tooltip("BGM 以外で同時に鳴らせる音の数。超えると一番古い音を止めて鳴らす")]
    [SerializeField]
    private int m_maxSources = 32;

    public float BgmFadeTime { get { return m_bgmFadeTime; } }
    public int MaxSources { get { return m_maxSources; } }

    // 種類ごとの音量 × 全体の音量
    public float GetVolume(SOUND_DATA.CATEGORY category)
    {
        float volume;

        switch (category)
        {
            case SOUND_DATA.CATEGORY.BGM:
                volume = m_bgmVolume;
                break;

            case SOUND_DATA.CATEGORY.UI:
                volume = m_uiVolume;
                break;

            case SOUND_DATA.CATEGORY.VOICE:
                volume = m_voiceVolume;
                break;

            default:
                volume = m_seVolume;
                break;
        }

        return volume * m_masterVolume;
    }

    void OnValidate()
    {
        m_bgmFadeTime = Mathf.Max(0f, m_bgmFadeTime);
        m_maxSources = Mathf.Max(1, m_maxSources);
    }
}
