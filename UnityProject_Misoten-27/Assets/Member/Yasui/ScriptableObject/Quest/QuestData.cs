using UnityEngine;

[CreateAssetMenu(fileName = "NewQuestData", menuName = "Game/Quest Data")]
public class QuestData : ScriptableObject
{
    public string questID;
    public string questTitle;
    public GroupType group; // クエストのグループ
    public PartData recommendedPart; // 適した部品（効率UP・追加pt用）
    public GameObject questPrefab; // 生成するプレハブ
}
