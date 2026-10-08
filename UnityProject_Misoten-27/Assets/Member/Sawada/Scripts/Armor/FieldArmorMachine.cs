using UnityEngine;
using UnityEngine.InputSystem;

public class FieldArmorMachine : MonoBehaviour
{
    [Header("対応アーマー")]
    [SerializeField] BaseArmorSO m_armorSO;

    public void Initialize(BaseArmorSO armorSO)
    {
        if (m_armorSO == null) return;

        m_armorSO = armorSO;

        // モデル反映
        Instantiate(m_armorSO.m_modelPrefab, transform);
    }

    private void Start()
    {
        Initialize(m_armorSO);
    }
}
