using UnityEngine;

//==================================================
// 高橋さんのミニゲームのスコアを GAME_SCORE に加点する橋渡し
//
// ・シーン内のミニゲーム（MiniGame を継承したもの）を自動で探す
// ・ミニゲームが終わった瞬間（GetIsFinish が true になった瞬間）に、
//   そのミニゲームのスコアを指定プレイヤーに加点する
// ・高橋さんのスクリプトは変更していない
//
// ※ 今は1人プレイ用（全部 m_playerIndex のプレイヤーに加点）。
//   4人対応するときは、ミニゲームを遊んだプレイヤーの番号を
//   MiniGameSystem から受け取る形に変える必要がある
//==================================================
public class GAME_MINIGAME_SCORE : MonoBehaviour
{
    [Tooltip("加点するプレイヤー（0 = 1P）")]
    [Range(0, GAME_SCORE.MAX_PLAYERS - 1)]
    [SerializeField]
    private int m_playerIndex = 0;

    private MiniGame[] m_miniGames;
    private bool[] m_wasFinished;

    void Start()
    {
        // 非表示のものも含めて探す
        m_miniGames =
            FindObjectsByType<MiniGame>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        m_wasFinished =
            new bool[m_miniGames.Length];

        for (int i = 0; i < m_miniGames.Length; i++)
        {
            m_wasFinished[i] = m_miniGames[i].GetIsFinish();
        }
    }

    // ミニゲーム側の Update で終了・スコアが決まった後に確認する
    void LateUpdate()
    {
        for (int i = 0; i < m_miniGames.Length; i++)
        {
            MiniGame miniGame = m_miniGames[i];

            if (miniGame == null)
            {
                continue;
            }

            bool isFinished = miniGame.GetIsFinish();

            // 終わった瞬間だけ加点する
            if (isFinished && !m_wasFinished[i])
            {
                int score = miniGame.GetScore();

                GAME_SCORE.AddScore(m_playerIndex, score);

                Debug.Log(
                    "ミニゲーム「" + miniGame.gameObject.name + "」の結果 " + score +
                    " 点を " + (m_playerIndex + 1) + "P に加点");
            }

            m_wasFinished[i] = isFinished;
        }
    }
}
