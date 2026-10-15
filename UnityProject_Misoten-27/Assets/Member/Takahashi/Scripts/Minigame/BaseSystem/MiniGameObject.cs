using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

//ミニゲーム用オブジェクト基底クラス
public class MiniGameObject : MonoBehaviour
{
    public virtual void Activate()
    {
        // 処理なし（継承先で記述）
    }

    public virtual void Deactivate() 
    {
        // 処理なし（継承先で記述）
    }

    public virtual void UpdateObject()
    {
        // 処理なし（継承先で記述）
    }
}
