using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSwapController : MonoBehaviour
{
    [SerializeField] private PlayerInput m_playerInput;
    private InputAction m_swapAction;

    [Header("Inventory")]
    [SerializeField] private PlayerInventory m_inventory;

    // swap可能一覧
    private List<FieldArmorMachine> m_canSwapArmor = new();

    // =========================================================
    // Public
    // =========================================================
    public bool PressSwap
    {
        get
        {
            return m_swapAction.WasPressedThisFrame();
        }
    }

    // 接触中アーマーからの信号
    public void AddCanSwapArmor(FieldArmorMachine addArmor)
    {
        // 未登録なら追加
        if (!m_canSwapArmor.Contains(addArmor)) m_canSwapArmor.Add(addArmor);
    }
    public void RemoveCanSwapArmor(FieldArmorMachine addArmor)
    {
        // 登録済みなら削除
        m_canSwapArmor.Remove(addArmor);
    }

    // =========================================================
    // Unity
    // =========================================================
    private void Awake()
    {
        m_swapAction = m_playerInput.actions.FindAction("Swap", true);
    }


    // =========================================================
    // Swap
    // =========================================================
    public bool Swap()
    {
        // 交換ボタンを押したら
        if (PressSwap && 0 < m_canSwapArmor.Count)
        {
            // 交換
            BaseArmorSO releaseArmor = m_inventory.SwapArmor(m_canSwapArmor[0].GetArmorSO());

            RemoveCanSwapArmor(m_canSwapArmor[0]);

            return true;
;       }
        return false;
    }


    // =========================================================
    // Private
    // =========================================================

}
