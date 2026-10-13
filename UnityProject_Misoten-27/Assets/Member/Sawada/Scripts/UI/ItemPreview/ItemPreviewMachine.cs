using UnityEngine;

public class ItemPreviewMachine : MonoBehaviour
{
    [SerializeField] Transform m_Camera1_1;
    [SerializeField] Transform m_Camera1_2;
    [SerializeField] Transform m_Camera2_1;
    [SerializeField] Transform m_Camera2_2;
    [SerializeField] Transform m_Camera3_1;
    [SerializeField] Transform m_Camera3_2;
    [SerializeField] Transform m_Camera4_1;
    [SerializeField] Transform m_Camera4_2;

    // 指定カメラの前にアーマーを配置する
    public void SetPreviewItem(GameObject armor, int num1, int num2)
    {
        // アーマーの親を決定
        Transform parent;
        switch (num1)
        {
            case 1:
                parent = (num2 == 1) ? m_Camera1_1 : m_Camera1_2;
                break;
            case 2:
                parent = (num2 == 1) ? m_Camera2_1 : m_Camera2_2;
                break;
            case 3:
                parent = (num2 == 1) ? m_Camera3_1 : m_Camera3_2;
                break;
            case 4:
                parent = (num2 == 1) ? m_Camera4_1 : m_Camera4_2;
                break;
            default:
                Debug.LogError("範囲外");
                return;
        }

        // 前のアーマーを削除
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }

        // アーマーを生成して配置
        if (armor != null)
        {
            GameObject obj = Instantiate(armor, parent);
            obj.transform.localPosition = new Vector3(0, 0, 5);
        }
    }
}
