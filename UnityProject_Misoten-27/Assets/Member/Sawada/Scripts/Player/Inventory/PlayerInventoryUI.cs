using System.Collections.Generic;
using UnityEngine;


/*
 * Kumei    26/10/14
 * 
 * アーマーインベントリ UI制御
 */
public class PlayerInventoryUI : MonoBehaviour
{
    private PlayerInventory m_playerInventory;

    [SerializeField] private List<GameObject> m_frontUIList;

    [SerializeField] private List<Camera> m_itemPreviewCameraList;

    private void Start()
    {
        m_playerInventory = GetComponent<PlayerInventory>();
    }

    // 特定のm_frontをON それ以外をOFFにする
    public void SetFrontUI(int index)
    {
        for (int i = 0; i < m_frontUIList.Count; i++)
        {
            m_frontUIList[i].SetActive(index == i);
        }
    }

    // 全m_frontをOFFにする
    public void SetAllFrontUIOff()
    {
        for (int i = 0; i < m_frontUIList.Count; i++)
        {
            m_frontUIList[i].SetActive(false);
        }
    }

    // アーマーの表示を更新する
    public void UpdateArmorDisplay(int index, BaseArmorSO armor)
    {
        if (index < 0 || index >= m_frontUIList.Count) return;

        // armorの情報をUIに反映する
        // 位置調整
        GameObject itemPreview = Instantiate(armor.m_modelPrefab, m_itemPreviewCameraList[index].transform);
        itemPreview.transform.localPosition = new Vector3(0, 0, 5);

        // カメラ表示設定
        ItemPreviewCameraVisibility camVisibility = itemPreview.AddComponent<ItemPreviewCameraVisibility>();
        camVisibility.SetTargetCamera(m_itemPreviewCameraList[index]);
    }
}
