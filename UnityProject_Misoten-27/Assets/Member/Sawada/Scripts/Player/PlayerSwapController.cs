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
    private List<FieldArmorMachine> m_canSwapArmor;

    // =========================================================
    // Public
    // =========================================================
    public bool PressSprint
    {
        get
        {
            return m_swapAction.IsPressed();
        }
    }

    // 接触中アーマーからの信号
    public void AddCanSwapArmor(FieldArmorMachine addArmor)
    {
        // 未登録なら追加
        if (!m_canSwapArmor.Contains(addArmor))
            m_canSwapArmor.Add(addArmor);
    }
    public void RemoveCanSwapArmor(FieldArmorMachine addArmor)
    {
        // 登録済みなら削除
        if (m_canSwapArmor.Contains(addArmor)) m_canSwapArmor.Remove(addArmor);
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
    public void Swap()
    {
        if (PressSprint && 0 < m_canSwapArmor.Count)
        {
            BaseArmorSO releaseArmor = m_inventory.SwapArmor(m_canSwapArmor[0].GetArmorSO());
            RemoveCanSwapArmor(m_canSwapArmor[0]);

            // オブジェクト化 飛び出す感じ
            Debug.Log("Swap");
;        }
    }


    // =========================================================
    // Private
    // =========================================================

}
