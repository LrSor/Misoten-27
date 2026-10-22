using System;
using UnityEngine;

//==================================================
// ゲーム全体のスコアを持つ場所（シーンをまたいで残る）
//
// ・どこからでも GAME_SCORE.AddScore(プレイヤー番号, 点数) で加点できる
// ・ゲーム中以外（準備中・タイムアップ後）は加点されない
// ・リザルトは GAME_SCORE.TotalScore を読む
//
// static クラスなのでシーンに置く必要はなく、
// シーンを移動しても値が消えない（タイトルに戻るときにリセットする）
//==================================================
public static class GAME_SCORE
{
    // プレイヤーの最大人数
    public const int MAX_PLAYERS = 4;

    private static readonly int[] s_playerScores =
        new int[MAX_PLAYERS];

    // true の間は加点されない（ゲーム開始前・タイムアップ後）
    public static bool IsLocked { get; private set; } = true;

    // 加点されたときに呼ばれる（引数：プレイヤー番号, 加点した点数）
    // スコア表示 UI などで使う
    public static event Action<int, int> ScoreAdded;

    // 全プレイヤーの合計スコア
    public static int TotalScore
    {
        get
        {
            int total = 0;

            foreach (int score in s_playerScores)
            {
                total += score;
            }

            return total;
        }
    }

    // ドメインリロード無しで再生した場合に前回の値が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Array.Clear(s_playerScores, 0, s_playerScores.Length);

        IsLocked = true;

        ScoreAdded = null;
    }

    //==================================================
    // 加点
    // playerIndex は 0 ～ 3（1P = 0）
    //==================================================

    public static void AddScore(int playerIndex, int amount)
    {
        if (IsLocked)
        {
            return;
        }

        if (playerIndex < 0 ||
            playerIndex >= MAX_PLAYERS)
        {
            Debug.LogWarning(
                "GAME_SCORE: プレイヤー番号が範囲外です: " + playerIndex);

            return;
        }

        s_playerScores[playerIndex] += amount;

        if (ScoreAdded != null)
        {
            ScoreAdded(playerIndex, amount);
        }
    }

    // プレイヤーごとのスコア
    public static int GetPlayerScore(int playerIndex)
    {
        if (playerIndex < 0 ||
            playerIndex >= MAX_PLAYERS)
        {
            return 0;
        }

        return s_playerScores[playerIndex];
    }

    // スコアを 0 に戻す（タイトル・ゲーム開始時に呼ぶ）
    public static void ResetScores()
    {
        Array.Clear(s_playerScores, 0, s_playerScores.Length);
    }

    // 加点の受付を止める / 再開する（GAME_MANAGER が呼ぶ）
    public static void SetLocked(bool isLocked)
    {
        IsLocked = isLocked;
    }
}
