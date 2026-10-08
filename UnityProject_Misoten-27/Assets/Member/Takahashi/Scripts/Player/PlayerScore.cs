using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerScore : MonoBehaviour
{
    [System.Serializable] private class Result
    {
        [Header("ミニゲーム")]
        [SerializeField] private MiniGame m_miniGame;
        private int m_score = 0;

        //コンストラクタ
        public Result(MiniGame miniGame, int score)
        {
            m_miniGame = miniGame;
            m_score = score;
        }

        public MiniGame GetMiniGame()
        { 
            return m_miniGame; 
        }

        public int GetScore()
        {
            return m_score;
        }

        public void UpdateScore(int newScore)
        {
            if(m_score < newScore)
            {
                m_score = newScore;
            }
        }

    }

    //ミニゲーム結果保存用リスト
    [SerializeField] private List<Result> m_results;

    [Header("スコア表示用テキストUI")]
    [SerializeField] private Text m_scoreText;

    private int m_totalScore = 0;

    void Start()
    {
        //リストのダミーデータを作成
        MiniGame dummyGame = new MiniGame();
        Result dummyResult = new Result(dummyGame, 0);
        m_results.Add(dummyResult);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetMiniGameScore(MiniGame miniGame, int  score)
    {
        m_totalScore = 0;
        bool isPlayed = false;
        foreach (Result result in m_results)
        {
            //今回プレイしたミニゲームのスコアを更新
            if(result.GetMiniGame() == miniGame)
            {
                result.UpdateScore(score);
                isPlayed = true;
            }
            //トータルのスコアに加算
            m_totalScore += result.GetScore();
        }

        //今回初めてプレイしたミニゲームだった場合、リストにミニゲームの情報を追加
        if(!isPlayed)
        {
            //新しいminiGame,scoreをlistに格納
            Result newResult = new Result(miniGame,score);
            m_results.Add(newResult);

            //トータルのスコアに加算
            m_totalScore += score;
        }

        //スコア表示UIを更新
        m_scoreText.text = m_totalScore.ToString("Total Score: 0000");

    }
}
