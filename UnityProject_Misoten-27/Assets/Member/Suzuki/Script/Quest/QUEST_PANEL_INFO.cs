using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//==================================================
// クエストのパネル（Liu さんの LiuQuestPanel）に、近くのクエストの中身を出す
//
// ・クエスト名と、そのクエストで遊ぶミニゲームの名前を出す
//   （QUEST_CHALLENGE が決めたミニゲームと同じものを出すので、表示と実際のゲームがずれない）
// ・おすすめの部品を出す
// ・プレビューの画像の場所に、そのミニゲームの画面を実際に写す
//   （ミニゲームのカメラと同じ位置に、パネル用のカメラを置いて写している）
//
// ・パネルの表示・非表示は、今まで通り Liu さんの LiuQuestPanelUI が決める
//   これはパネルが出ている間だけ、中身を書きかえる
// ・Liu さんのスクリプトは変更していない
//
// ・シーンに 1 つ置く（QuestSystem に付けている）
//==================================================
public class QUEST_PANEL_INFO : MonoBehaviour
{
    [Serializable]
    public class MINIGAME_NAME
    {
        [Tooltip("ミニゲーム")]
        public MiniGameSystem m_miniGame;

        [Tooltip("パネルに出す名前")]
        public string m_title;
    }

    [Header("参照")]
    [SerializeField]
    private QUEST_CHALLENGE m_challenge;

    [Tooltip("クエストのパネル（これが表示されている間だけ中身を書きかえる）")]
    [SerializeField]
    private GameObject m_panelRoot;

    [Header("パネルの中身")]
    [Tooltip("クエスト名とミニゲーム名を出す文字")]
    [SerializeField]
    private TMP_Text m_titleText;

    [Tooltip("おすすめの部品を出す文字（空欄でもよい）")]
    [SerializeField]
    private TMP_Text m_partText;

    [Tooltip("ミニゲームの画面を写す場所（この中いっぱいに写す）")]
    [SerializeField]
    private RectTransform m_previewArea;

    [Header("ミニゲームの名前")]
    [SerializeField]
    private List<MINIGAME_NAME> m_miniGameNames = new List<MINIGAME_NAME>();

    [Header("探し方・写し方")]
    [Tooltip("プレイヤーからこの距離（m）以内で、いちばん近いクエストを出す")]
    [SerializeField]
    private float m_searchDistance = 8f;

    [Tooltip("写す画面の大きさ（横、縦）")]
    [SerializeField]
    private Vector2Int m_previewResolution = new Vector2Int(640, 360);

    private Camera m_previewCamera;
    private RenderTexture m_renderTexture;
    private RawImage m_previewImage;

    private Quest m_currentQuest;
    private MiniGameSystem m_currentMiniGame;
    private Camera m_sourceCamera;
    private MINIGAME_CAMERA m_sourceMiniGameCamera;

    void Awake()
    {
        if (m_challenge == null)
        {
            m_challenge = FindFirstObjectByType<QUEST_CHALLENGE>();
        }
    }

    void OnDestroy()
    {
        if (m_previewCamera != null)
        {
            Destroy(m_previewCamera.gameObject);
        }

        if (m_renderTexture != null)
        {
            m_renderTexture.Release();
            Destroy(m_renderTexture);
        }
    }

    void LateUpdate()
    {
        bool isShown =
            m_panelRoot != null &&
            m_panelRoot.activeInHierarchy &&
            m_challenge != null &&
            !m_challenge.IsChallenging;

        Quest quest = isShown ? m_challenge.FindClosestQuest(m_searchDistance) : null;

        if (quest == null)
        {
            SetPreviewActive(false);
            m_currentQuest = null;
            return;
        }

        MiniGameSystem miniGame = m_challenge.GetMiniGame(quest);

        if (quest != m_currentQuest || miniGame != m_currentMiniGame)
        {
            m_currentQuest = quest;
            m_currentMiniGame = miniGame;
            UpdateTexts(quest, miniGame);
            FindSourceCamera(miniGame);
        }

        UpdatePreview();
    }

    //==================================================
    // 文字
    //==================================================

    void UpdateTexts(Quest quest, MiniGameSystem miniGame)
    {
        string questTitle = quest.Data != null ? quest.Data.questTitle : quest.name;
        string gameTitle = GetMiniGameTitle(miniGame);

        if (m_titleText != null)
        {
            // クエスト名とミニゲーム名が同じなら 1 つだけ出す
            if (string.IsNullOrEmpty(gameTitle) || gameTitle == questTitle)
            {
                m_titleText.text = questTitle;
            }
            else
            {
                m_titleText.text = questTitle + "\n<size=70%>ミニゲーム：" + gameTitle + "</size>";
            }
        }

        if (m_partText != null)
        {
            PartData part = quest.Data != null ? quest.Data.recommendedPart : null;

            m_partText.text = part != null
                ? "おすすめの部品：" + (string.IsNullOrEmpty(part.partName) ? part.name : part.partName)
                : "おすすめの部品：なし";
        }
    }

    string GetMiniGameTitle(MiniGameSystem miniGame)
    {
        if (miniGame == null)
        {
            return "（未設定）";
        }

        foreach (MINIGAME_NAME entry in m_miniGameNames)
        {
            if (entry != null && entry.m_miniGame == miniGame && !string.IsNullOrEmpty(entry.m_title))
            {
                return entry.m_title;
            }
        }

        return miniGame.gameObject.name;
    }

    //==================================================
    // ミニゲームの画面を写す
    //==================================================

    void FindSourceCamera(MiniGameSystem miniGame)
    {
        m_sourceCamera = null;
        m_sourceMiniGameCamera = null;

        if (miniGame == null)
        {
            return;
        }

        m_sourceMiniGameCamera = miniGame.GetComponentInChildren<MINIGAME_CAMERA>(true);

        m_sourceCamera = m_sourceMiniGameCamera != null
            ? m_sourceMiniGameCamera.GetComponent<Camera>()
            : miniGame.GetComponentInChildren<Camera>(true);
    }

    void UpdatePreview()
    {
        if (m_previewArea == null || m_sourceCamera == null)
        {
            SetPreviewActive(false);
            return;
        }

        CreatePreview();
        SetPreviewActive(true);

        // ミニゲームのカメラと同じ設定・同じ位置で写す
        Vector3 position;
        Quaternion rotation;
        float fov;

        if (m_sourceMiniGameCamera != null)
        {
            m_sourceMiniGameCamera.GetViewPose(out position, out rotation);
            fov = m_sourceMiniGameCamera.DefaultFov;
        }
        else
        {
            position = m_sourceCamera.transform.position;
            rotation = m_sourceCamera.transform.rotation;
            fov = m_sourceCamera.fieldOfView;
        }

        m_previewCamera.transform.SetPositionAndRotation(position, rotation);
        m_previewCamera.fieldOfView = fov;
        m_previewCamera.nearClipPlane = m_sourceCamera.nearClipPlane;
        m_previewCamera.farClipPlane = m_sourceCamera.farClipPlane;
        m_previewCamera.clearFlags = m_sourceCamera.clearFlags;
        m_previewCamera.backgroundColor = m_sourceCamera.backgroundColor;
        m_previewCamera.cullingMask = m_sourceCamera.cullingMask;
    }

    void CreatePreview()
    {
        if (m_renderTexture == null)
        {
            m_renderTexture = new RenderTexture(
                Mathf.Max(16, m_previewResolution.x),
                Mathf.Max(16, m_previewResolution.y),
                24);
            m_renderTexture.name = "QuestPreview";
        }

        if (m_previewCamera == null)
        {
            GameObject cameraObject = new GameObject("QuestPreviewCamera");
            m_previewCamera = cameraObject.AddComponent<Camera>();
            m_previewCamera.targetTexture = m_renderTexture;
            m_previewCamera.depth = -10f;
        }

        if (m_previewImage == null)
        {
            // プレビューの場所いっぱいに、写した画面を出す
            GameObject imageObject = new GameObject("QuestPreviewRender", typeof(RectTransform));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(m_previewArea, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            m_previewImage = imageObject.AddComponent<RawImage>();
            m_previewImage.texture = m_renderTexture;
            m_previewImage.raycastTarget = false;
        }
    }

    void SetPreviewActive(bool isActive)
    {
        if (m_previewCamera != null && m_previewCamera.enabled != isActive)
        {
            m_previewCamera.enabled = isActive;
        }

        if (m_previewImage != null && m_previewImage.gameObject.activeSelf != isActive)
        {
            m_previewImage.gameObject.SetActive(isActive);
        }
    }

    void OnValidate()
    {
        m_searchDistance = Mathf.Max(0.5f, m_searchDistance);
    }
}
