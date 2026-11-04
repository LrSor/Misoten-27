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

    public int CurrentArmorIndex => m_currentArmorIndex;
    public int ArmorSlotCount => m_armorSlots.Count;
    public BaseArmorSO GetArmorAt(int index) => m_armorSlots[index];

    // Network instances keep this component's keyboard Update disabled. The Host calls
    // the same slot-selection and fitting methods used by offline Play.
    public void ConfigureNetworkInventory(BaseArmorSO[] initialSlots)
    {
        m_playerInventoryUI = GetComponent<PlayerInventoryUI>();
        if (initialSlots != null && initialSlots.Length > 0)
            m_armorSlots = new List<BaseArmorSO>(initialSlots);
        for (int i = 0; i < m_armorSlots.Count; i++)
        {
            m_playerInventoryUI?.ClearItemPreview(i);
            m_playerInventoryUI?.UpdateArmorDisplay(i, m_armorSlots[i]);
        }
        SelectNetworkArmor(m_currentArmorIndex);
    }

    public void SelectNetworkArmor(int index)
    {
        if (index < 0 || index >= m_armorSlots.Count) return;
        m_currentArmorIndex = index;
        m_playerInventoryUI?.SetFrontUI(index);
        m_characterAmorController.FitArmor(m_armorSlots[index]);
    }

    public void ApplyNetworkInventory(int selectedIndex, BaseArmorSO[] slots)
    {
        bool changed = m_currentArmorIndex != selectedIndex || m_armorSlots.Count != slots.Length;
        for (int i = 0; !changed && i < slots.Length; i++) changed = m_armorSlots[i] != slots[i];
        if (!changed) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < m_armorSlots.Count && m_armorSlots[i] == slots[i]) continue;
            m_playerInventoryUI?.ClearItemPreview(i);
            m_playerInventoryUI?.UpdateArmorDisplay(i, slots[i]);
        }
        m_armorSlots = new List<BaseArmorSO>(slots);
        SelectNetworkArmor(selectedIndex);
    }


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
