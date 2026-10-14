using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


/*
 * Kumei    26/10/14
 * 
 * アーマーインベントリ
 */
public class PlayerInventory : MonoBehaviour
{
    [Header("アーマー枠(基本2つまで)")]
    [SerializeField] private List<BaseArmorSO> m_armorSlots;
    int m_currentArmorIndex = 0;    // 現在装備中のアーマーのインデックス



    private void Update()
    {
        // 他スロットがある場合のみ変更
        if (m_armorSlots.Count <= 1) return;
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            ChangeArmor(-1);
            GetComponent<PlayerInventoryUI>().testKirikae();
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            ChangeArmor(1);
            GetComponent<PlayerInventoryUI>().testKirikae();
        }
    }

    // 現在の装備中のアーマー
    public BaseArmorSO GetCurrentArmor()
    {
        if (m_armorSlots.Count > 0 && m_currentArmorIndex >= 0 && m_currentArmorIndex < m_armorSlots.Count)
            return m_armorSlots[m_currentArmorIndex];
        return null;
    }

    // アーマーを変更
    public void ChangeArmor(int addIndex)
    {
        m_currentArmorIndex += addIndex;
        if (m_currentArmorIndex < 0)
            m_currentArmorIndex = m_armorSlots.Count - 1;
        else if (m_currentArmorIndex == m_armorSlots.Count)
            m_currentArmorIndex = 0;
    }

    // アーマーを入れ替える
    public BaseArmorSO SwapArmor(BaseArmorSO newArmor)
    {
        BaseArmorSO oldArmor = GetCurrentArmor();
        if (oldArmor != null)
        {
            m_armorSlots[m_currentArmorIndex] = newArmor;
        }
        return oldArmor;
    }
}
