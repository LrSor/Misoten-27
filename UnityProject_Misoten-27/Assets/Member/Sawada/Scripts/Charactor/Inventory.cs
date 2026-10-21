using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;


/*
 * Kumei    26/10/14
 * 
 * アーマーインベントリ
 */
public class Inventory : MonoBehaviour
{
    [Header("アーマー枠(基本2つまで)")]
    [SerializeField] private List<BaseArmorSO> m_armorSlots;
    private int m_currentArmorIndex = 0;    // 現在装備中のアーマーのインデックス

    [SerializeField] private CharacterAmorController m_characterAmorController;

    private PlayerInventoryUI m_playerInventoryUI;


    private void Start()
    {
        m_playerInventoryUI = GetComponent<PlayerInventoryUI>();
    }

    private void Update()
    {
        // 他スロットがある場合のみ変更   PlayerInputへ変更直し
        //if (m_armorSlots.Count <= 1) return;
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            // nullではない要素が2つ以上ある場合のみ変更
            /*if (m_armorSlots.Count(armor => armor != null) >= 2)*/ ArmorLeftShift();
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            /*if (m_armorSlots.Count(armor => armor != null) >= 2)*/ ArmorRightShift();
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
    public void ArmorLeftShift()
    {
        // 左に変更
        m_currentArmorIndex -= 1;
        if (m_currentArmorIndex < 0) m_currentArmorIndex = m_armorSlots.Count - 1;

        // UI更新
        m_playerInventoryUI?.SetFrontUI(m_currentArmorIndex);

        // 仮 上に出す   本番は着る
        m_characterAmorController.FitArmor(m_armorSlots[m_currentArmorIndex]);
    }
    public void ArmorRightShift()
    {
        // 右に変更
        m_currentArmorIndex += 1;
        if (m_armorSlots.Count <= m_currentArmorIndex) m_currentArmorIndex = 0;

        // UI更新
        m_playerInventoryUI?.SetFrontUI(m_currentArmorIndex);

        // 仮 上に出す   本番は着る
        m_characterAmorController.FitArmor(m_armorSlots[m_currentArmorIndex]);
    }


    // アーマーを入れ替える
    public BaseArmorSO SwapArmor(BaseArmorSO newArmor)
    {
        BaseArmorSO oldArmor = GetCurrentArmor();

        // 古いアーマーの3DUIを削除
        if (oldArmor != null)
        {
            m_playerInventoryUI?.ClearItemPreview(m_currentArmorIndex);
        }

        // 上書き
        OverrideCurrentArmor(newArmor);

        // m_frontをON
        m_playerInventoryUI?.SetFrontUI(m_currentArmorIndex);

        // 仮 上に出す   本番は着る
        m_characterAmorController.FitArmor(m_armorSlots[m_currentArmorIndex]);

        return oldArmor;
    }

    // 現在のアーマーを上書きする
    public void OverrideCurrentArmor(BaseArmorSO newArmor)
    {
        // 上書き
        m_armorSlots[m_currentArmorIndex] = newArmor;

        // UI3Dオブジェクト更新
        m_playerInventoryUI?.UpdateArmorDisplay(m_currentArmorIndex, newArmor);
    }
}
