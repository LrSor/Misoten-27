using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//==================================================
// クエストに近づいてボタンを押すと、ミニゲームに挑戦できるようにする
//
//   1. プレイヤーがクエスト（Liu さんのクエストの「!」）の近くに行く
//   2. 挑戦ボタンを押す（キーボード：Z / Joy-Con：ZL）
//   3. そのクエストに対応した高橋さんのミニゲームが始まる
//   4. ミニゲームが終わってプレイヤーに戻ってきたら、クエストをクリアにする
//      → Liu さんの LiuQuestManager が、同じグループに次のクエストを出す
//
// ・シーンに 1 つ置く（QuestSystem に付けている）
// ・どのクエストでどのミニゲームをするかは「クエストとミニゲームの対応」で決める。
//   表にないクエストは「対応が決まっていないときのミニゲーム」からランダムに選ぶ
// ・ミニゲームのスコアは今まで通り GAME_MINIGAME_SCORE が加点する
// ・高橋さん・Liu さん・安井さんのスクリプトは変更していない
//
// ※ 今は1人プレイ用（Player タグで Rigidbody を持つ最初のプレイヤーを使う）
//==================================================
public class QUEST_CHALLENGE : MonoBehaviour
{
    [Serializable]
    public class QUEST_MINIGAME
    {
        [Tooltip("クエストのデータ")]
        public QuestData m_quest;

        [Tooltip("そのクエストで遊ぶミニゲーム")]
        public MiniGameSystem m_miniGame;
    }

    // 挑戦を始めた・クリアしたときの通知（他のスクリプトから使える）
    public static event Action<Quest> ChallengeStarted;
    public static event Action<Quest> ChallengeCleared;

    [Header("クエストとミニゲームの対応")]
    [SerializeField]
    private List<QUEST_MINIGAME> m_links = new List<QUEST_MINIGAME>();

    [Tooltip("上の表にないクエストのとき、この中からランダムに選ぶ")]
    [SerializeField]
    private MiniGameSystem[] m_fallbackMiniGames;

    [Header("操作")]
    [Tooltip("クエストからこの距離（m）以内なら挑戦できる")]
    [SerializeField]
    private float m_challengeDistance = 2.5f;

    [Tooltip("挑戦するキー（キーボード）")]
    [SerializeField]
    private Key m_challengeKey = Key.Z;

    [Tooltip("挑戦するボタン（左 Joy-Con）")]
    [SerializeField]
    private Joycon.Button m_joyconButton = Joycon.Button.SHOULDER_2;

    [Header("表示")]
    [Tooltip("挑戦できるときに、画面の下に案内を出す（仮の表示）")]
    [SerializeField]
    private bool m_showPrompt = true;

    // プレイヤーの体（澤田さんの Player の Mainbody）
    private GameObject m_player;

    // 挑戦できる距離にある、いちばん近いクエスト
    private Quest m_nearestQuest;

    // 挑戦中のクエスト
    private Quest m_challengingQuest;
    private bool m_isChallenging;

    private GUIStyle m_promptStyle;

    public bool IsChallenging
    {
        get { return m_isChallenging; }
    }

    // ドメインリロード無しで再生した場合に前回の登録が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ChallengeStarted = null;
        ChallengeCleared = null;
    }

    void Update()
    {
        if (m_isChallenging)
        {
            UpdateChallenge();
            return;
        }

        m_nearestQuest = null;

        // プレイ中だけ挑戦できる（GAME_MANAGER がないシーンではいつでも）
        if (GAME_MANAGER.Instance != null &&
            !GAME_MANAGER.Instance.IsPlaying)
        {
            return;
        }

        if (!FindPlayer())
        {
            return;
        }

        m_nearestQuest = FindNearestQuest();

        if (m_nearestQuest != null &&
            IsChallengePressed())
        {
            StartChallenge(m_nearestQuest);
        }
    }

    void OnDisable()
    {
        // ロック中（カウントダウン中・タイムアップ後）は案内を消す
        m_nearestQuest = null;
    }

    //==================================================
    // 挑戦
    //==================================================

    void StartChallenge(Quest quest)
    {
        MiniGameSystem miniGame = GetMiniGame(quest);

        if (miniGame == null)
        {
            Debug.LogWarning(
                "QUEST_CHALLENGE：クエスト「" + GetTitle(quest) + "」で遊ぶミニゲームが設定されていません");
            return;
        }

        m_challengingQuest = quest;
        m_isChallenging = true;
        m_nearestQuest = null;

        Debug.Log(
            "クエスト「" + GetTitle(quest) + "」に挑戦：" + miniGame.gameObject.name);

        // 高橋さんのミニゲームを始める（プレイヤーは一時的に非表示になる）
        miniGame.Activate(m_player);

        if (ChallengeStarted != null)
        {
            ChallengeStarted(quest);
        }
    }

    // ミニゲームが終わると、MiniGameSystem がプレイヤーを表示に戻す
    // → それを見てクエストをクリアにする
    void UpdateChallenge()
    {
        if (m_player == null ||
            !m_player.activeSelf)
        {
            return;
        }

        m_isChallenging = false;

        Quest quest = m_challengingQuest;
        m_challengingQuest = null;

        // 挑戦中に別の理由で消えていた場合は何もしない
        if (quest == null)
        {
            return;
        }

        Debug.Log("クエスト「" + GetTitle(quest) + "」クリア");

        if (ChallengeCleared != null)
        {
            ChallengeCleared(quest);
        }

        // Liu さんの LiuQuestManager が次のクエストを出す
        quest.ClearQuest();
    }

    MiniGameSystem GetMiniGame(Quest quest)
    {
        if (quest.Data != null)
        {
            foreach (QUEST_MINIGAME link in m_links)
            {
                if (link != null &&
                    link.m_quest == quest.Data &&
                    link.m_miniGame != null)
                {
                    return link.m_miniGame;
                }
            }
        }

        // 表にない場合はランダム
        List<MiniGameSystem> candidates = new List<MiniGameSystem>();

        if (m_fallbackMiniGames != null)
        {
            foreach (MiniGameSystem miniGame in m_fallbackMiniGames)
            {
                if (miniGame != null)
                {
                    candidates.Add(miniGame);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    //==================================================
    // プレイヤーとクエストを探す
    //==================================================

    // Player タグが付いていて、実際に動く体（Rigidbody 付き）を探す
    bool FindPlayer()
    {
        if (m_player != null)
        {
            return m_player.activeInHierarchy;
        }

        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (player.GetComponent<Rigidbody>() != null)
            {
                m_player = player;
                return true;
            }
        }

        return false;
    }

    Quest FindNearestQuest()
    {
        Quest[] quests =
            FindObjectsByType<Quest>(FindObjectsSortMode.None);

        Vector3 playerPosition = m_player.transform.position;

        Quest nearest = null;
        float nearestDistance = m_challengeDistance;

        foreach (Quest quest in quests)
        {
            // 高さは無視して、地面の上での距離で比べる
            Vector3 offset = quest.transform.position - playerPosition;
            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = quest;
            }
        }

        return nearest;
    }

    //==================================================
    // 入力
    //==================================================

    bool IsChallengePressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard[m_challengeKey].wasPressedThisFrame)
        {
            return true;
        }

        Joycon joycon = GetLeftJoycon();

        return joycon != null &&
               joycon.GetButtonDown(m_joyconButton);
    }

    static Joycon GetLeftJoycon()
    {
        if (JoyconManager.Instance == null ||
            JoyconManager.Instance.j == null)
        {
            return null;
        }

        return JoyconManager.Instance.j.Find(c => c.isLeft);
    }

    static string GetTitle(Quest quest)
    {
        return quest.Data != null ? quest.Data.questTitle : quest.name;
    }

    //==================================================
    // 仮の案内表示
    //==================================================

    void OnGUI()
    {
        if (!m_showPrompt ||
            m_nearestQuest == null ||
            m_isChallenging ||
            !enabled ||
            GAME_SCENE_LOADER.IsTransitioning)
        {
            return;
        }

        if (m_promptStyle == null)
        {
            m_promptStyle = new GUIStyle(GUI.skin.label);
            m_promptStyle.alignment = TextAnchor.MiddleCenter;
            m_promptStyle.fontStyle = FontStyle.Bold;
            m_promptStyle.normal.textColor = Color.yellow;
        }

        m_promptStyle.fontSize =
            Mathf.Max(12, Mathf.RoundToInt(40f * Screen.height / 1080f));

        // 文字化けしないように英語で表示する
        string text =
            "[" + m_challengeKey + " / ZL]  CHALLENGE";

        Vector2 size =
            m_promptStyle.CalcSize(new GUIContent(text));

        float padding = m_promptStyle.fontSize * 0.4f;

        Rect rect =
            new Rect(
                (Screen.width - size.x) * 0.5f - padding,
                Screen.height * 0.8f - padding,
                size.x + padding * 2f,
                size.y + padding * 2f);

        Color oldColor = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = oldColor;

        GUI.Label(rect, text, m_promptStyle);
    }

    void OnValidate()
    {
        m_challengeDistance = Mathf.Max(0.5f, m_challengeDistance);
    }
}
