using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


/*
 * Kumei    26/10/14
 * 
 * アーマーインベントリ UI制御
 */
public class PlayerInventoryUI : MonoBehaviour
{
    private Inventory m_playerInventory;

    [SerializeField] private List<GameObject> m_frontUIList;

    [SerializeField] private List<Camera> m_itemPreviewCameraList;
    readonly List<RenderTexture> m_networkTextures = new();
    readonly List<RawImage> m_networkImages = new();

    public void ConfigureNetworkDisplay(Canvas display)
    {
        // This existing HUD was laid out at 1920x1080 with a fixed pixel offset.
        // Scale it to the current window so its bottom slots remain on-screen.
        var scaler = display.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
        }
        var children = display.GetComponentsInChildren<Transform>(true);
        m_frontUIList = new List<GameObject>();
        for (int i = 0; i < m_itemPreviewCameraList.Count; i++)
        {
            var front = children.FirstOrDefault(t => t.name == "front" + (i + 1));
            var slot = children.FirstOrDefault(t => t.name == "slot" + (i + 1));
            var image = slot != null ? slot.GetComponent<RawImage>() : null;
            var camera = m_itemPreviewCameraList[i];
            if (front == null || image == null || camera == null)
                throw new System.InvalidOperationException("PlayerDisplay preview slot references are missing");
            m_frontUIList.Add(front.gameObject);
            // Separate runtime textures prevent slots and participants from overwriting each other.
            var texture = camera.targetTexture != null ? new RenderTexture(camera.targetTexture)
                : new RenderTexture(256, 256, 24);
            texture.name = "Network armor preview " + (i + 1);
            texture.hideFlags = HideFlags.DontSave;
            m_networkTextures.Add(texture);
            m_networkImages.Add(image);
            camera.targetTexture = texture;
            // The player prefab keeps the shared ItemPreview parent inactive.
            camera.transform.parent.gameObject.SetActive(true);
            camera.gameObject.SetActive(true);
            camera.enabled = true;
            image.texture = texture;
        }
        display.targetDisplay = 0;
    }

    void OnDestroy()
    {
        for (int i = 0; i < m_networkImages.Count; i++)
            if (m_networkImages[i] != null && m_networkImages[i].texture == m_networkTextures[i])
                m_networkImages[i].texture = null;
        foreach (var texture in m_networkTextures)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    private void Start()
    {
        m_playerInventory = GetComponent<Inventory>();
    }

    // 特定のm_frontをOFF それ以外をONにする
    public void SetFrontUI(int index)
    {
        for (int i = 0; i < m_frontUIList.Count; i++)
        {
            m_frontUIList[i].SetActive(index != i);
        }
    }

    // 全m_frontをONにする
    public void SetAllFrontUION()
    {
        for (int i = 0; i < m_frontUIList.Count; i++)
        {
            m_frontUIList[i].SetActive(true);
        }
    }

    // アーマーの表示を更新する
    public void UpdateArmorDisplay(int index, BaseArmorSO armor)
    {
        if (index < 0 || index >= m_frontUIList.Count) return;

        // armorの情報をUIに反映する
        if (armor != null)
        {
            // 位置調整
            GameObject itemPreview = Instantiate(armor.m_modelPrefab, m_itemPreviewCameraList[index].transform);
            itemPreview.transform.localPosition = new Vector3(0, 0, 5);

            // カメラ表示設定
            ItemPreviewCameraVisibility camVisibility = itemPreview.AddComponent<ItemPreviewCameraVisibility>();
            camVisibility.SetTargetCamera(m_itemPreviewCameraList[index]);
        }
    }

    // 指定カメラの子オブジェクトを削除する
    public void ClearItemPreview(int index)
    {
        if (index < 0 || index >= m_itemPreviewCameraList.Count) return;

        Camera targetCamera = m_itemPreviewCameraList[index];
        foreach (Transform child in targetCamera.transform)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }
}
