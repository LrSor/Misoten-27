using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

//ミニゲーム用オブジェクト基底クラス
public class MiniGameObject : MonoBehaviour
{
    public bool IsNetworkControlled { get; private set; } // 螟画峩 Sawada
    public bool IsNetworkAuthority { get; private set; }
    protected MiniGameNetworkInput NetworkInput { get; private set; }
    public void ConfigureNetwork(bool controlled, bool authority)
    {
        if (IsNetworkControlled == controlled && IsNetworkAuthority == authority) return;
        IsNetworkControlled = controlled;
        IsNetworkAuthority = authority;
        NetworkInput = default;
    }
    public void SetNetworkInput(MiniGameNetworkInput input) => NetworkInput = input;

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
