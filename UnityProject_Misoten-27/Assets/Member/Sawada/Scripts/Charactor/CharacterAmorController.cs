using UnityEngine;

public class CharacterAmorController : MonoBehaviour
{
    [SerializeField] private Transform m_parent;
    [SerializeField, Tooltip("未設定時は自身または親の Model/Wardrobe/TemporaryOutfit を使用します。")]
    private Transform m_temporaryOutfit;
    private BaseArmorSO m_currentArmorSO;

    // プレイヤーへ反映
    public BaseArmorSO FitArmor(BaseArmorSO armorSO)
    {
        BaseArmorSO oldArmorSO = m_currentArmorSO;
        m_currentArmorSO = armorSO;

        ReplaceTemporaryOutfit(armorSO);

        // 子オブジェクト削除
        if (m_parent != null && m_parent != m_temporaryOutfit)
        {
            foreach (Transform child in m_parent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (armorSO != null && armorSO.m_KARI_icon != null)
                Instantiate(armorSO.m_KARI_icon, m_parent);
        }

        return oldArmorSO;
    }

    private void ReplaceTemporaryOutfit(BaseArmorSO armorSO)
    {
        if (m_temporaryOutfit == null)
        {
            for (Transform owner = transform; owner != null; owner = owner.parent)
            {
                m_temporaryOutfit = owner.Find("Model/Wardrobe/TemporaryOutfit");
                if (m_temporaryOutfit != null) break;
            }
        }

        if (m_temporaryOutfit == null)
        {
            Debug.LogWarning("TemporaryOutfit が見つかりません。装備先の Transform を設定してください。", this);
            return;
        }

        // Destroy はフレーム末まで遅延するため、先に非表示にしてスロットから外す。
        for (int i = m_temporaryOutfit.childCount - 1; i >= 0; i--)
        {
            Transform child = m_temporaryOutfit.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        if (armorSO != null && armorSO.m_temporaryOutfit != null)
        {
            foreach (GameObject model in armorSO.m_temporaryOutfit)
            {
                if (model != null) Instantiate(model, m_temporaryOutfit, false);
            }
        }

        var slot = m_temporaryOutfit.GetComponent<Sawada.Cordex.AutoRigSlot>();
        if (slot != null) slot.Synchronize();
    }
}
