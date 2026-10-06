using UnityEngine;

[CreateAssetMenu(fileName = "NewPartData", menuName = "Game/Part Data")]
public class PartData : ScriptableObject
{
    public string partID;
    public string partName;
    public GroupType group; // 部品のグループ
    public GameObject partPrefab; // 生成するプレハブ
}
