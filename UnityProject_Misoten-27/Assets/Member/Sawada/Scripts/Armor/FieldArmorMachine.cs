using UnityEngine;

public class FieldArmorMachine : MonoBehaviour
{
    [Header("対応アーマー")]
    [SerializeField] BaseArmorSO m_armorSO;

    public void Initialize(BaseArmorSO armorSO)
    {
        if (m_armorSO == null) return;

        m_armorSO = armorSO;

        // モデル反映
        Instantiate(m_armorSO.m_KARI_icon, transform);
    }

    private void Start()
    {
        Initialize(m_armorSO);
    }

    public BaseArmorSO GetArmorSO()
    {
        return m_armorSO;
    }
}
