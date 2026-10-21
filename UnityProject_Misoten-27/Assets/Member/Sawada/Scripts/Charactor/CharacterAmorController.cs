using UnityEngine;

public class CharacterAmorController : MonoBehaviour
{
    [SerializeField] private Transform m_parent;
    private BaseArmorSO m_currentArmorSO;

    // プレイヤーへ反映
    public BaseArmorSO FitArmor(BaseArmorSO armorSO)
    {
        BaseArmorSO oldArmorSO = m_currentArmorSO;
        m_currentArmorSO = armorSO;

        // 子オブジェクト削除
        foreach (Transform child in m_parent)
        {
            Destroy(child.gameObject);
        }

        if (armorSO == null) return oldArmorSO;

        // 子オブジェクトを作成
        Instantiate(armorSO.m_modelPrefab, m_parent);

        return oldArmorSO;
    }
}
