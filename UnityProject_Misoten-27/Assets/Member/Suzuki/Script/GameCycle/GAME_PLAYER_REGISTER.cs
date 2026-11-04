using System.Collections.Generic;
using UnityEngine;

//==================================================
// シーンに直接置いたプレイヤーを、安井さんの PlayerSpawnManager に登録する（仮）
//
// ・Liu さんのクエスト（LiuQuestProximity）は
//   「PlayerSpawnManager が生成したプレイヤー」だけをプレイヤーとして扱う
// ・今は澤田さんの Player をシーンに直接置いているので、そのままだと
//   クエストに近づいても反応しない → ここで登録して反応するようにする
//
// ・PlayerSpawnManager の「Auto Spawn On Start」はオフにしておくこと
//   （オンだと別のプレイヤーが生成され、登録したプレイヤーが消される）
//
// ※ PlayerSpawnManager に登録用の関数がないため、中のリストに直接追加している。
//   最終的にプレイヤーを PlayerSpawnManager で生成するようになったら、これは外す
//==================================================
public class GAME_PLAYER_REGISTER : MonoBehaviour
{
    [Tooltip("登録するプレイヤー（いちばん上の親オブジェクト）")]
    [SerializeField]
    private GameObject[] m_players;

    void Start()
    {
        PlayerSpawnManager manager = PlayerSpawnManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning("GAME_PLAYER_REGISTER：PlayerSpawnManager がシーンにありません");
            return;
        }

        // SpawnedPlayers は読み取り専用として公開されているが、中身は List なので追加できる
        List<GameObject> spawnedPlayers =
            manager.SpawnedPlayers as List<GameObject>;

        if (spawnedPlayers == null)
        {
            Debug.LogWarning("GAME_PLAYER_REGISTER：PlayerSpawnManager の仕組みが変わったため登録できません");
            return;
        }

        foreach (GameObject player in m_players)
        {
            if (player != null &&
                !spawnedPlayers.Contains(player))
            {
                spawnedPlayers.Add(player);

                Debug.Log("GAME_PLAYER_REGISTER：" + player.name + " をプレイヤーとして登録しました");
            }
        }
    }
}
