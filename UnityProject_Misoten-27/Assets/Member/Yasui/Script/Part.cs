using UnityEngine;
using System;

public class Part : MonoBehaviour
{
    public PartData Data { get; private set; }

    // 部品が取得（回収）された時に発行するイベント（引数：取得されたPart）
    public static event Action<Part> OnPartPickedUp;

    public void Initialize(PartData data)
    {
        Data = data;
    }

    // プレイヤーが部品を拾った時に呼び出す処理
    public void PickUp()
    {
        // イベント通知
        OnPartPickedUp?.Invoke(this);

        // オブジェクトの破棄
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (Data == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.8f);

#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.cyan;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 8;

        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"【Part】\n{Data.partName}", style);
#endif
    }
}
