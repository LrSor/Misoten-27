using UnityEngine;
using UnityEngine.UI;

public class BlackCatImage : MiniGameObject
{
    //============================================================
    // メンバ変数
    //============================================================


    //オブジェクト初期化関数
    public override void Activate()
    {
        gameObject.SetActive(true);
    }

    //オブジェクト終了関数
    public override void Deactivate()
    {
        gameObject.SetActive(false);
    }

    //オブジェクト更新関数
    public override void UpdateObject()
    {
        
    }
}
