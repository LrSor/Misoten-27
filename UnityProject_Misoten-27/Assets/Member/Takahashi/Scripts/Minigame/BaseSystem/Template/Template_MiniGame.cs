using UnityEngine;

//ミニゲーム本体のテンプレート
public class Template_MiniGame : MiniGame
{
    protected override void Start()
    {
        //基底クラス内処理
        base.Start();

        //以下に必要な処理を追加

    }

    protected override void UpdateMiniGame()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの実行)
        base.UpdateMiniGame();

        //以下に必要な処理を追加

    }

    public override void Activate()
    {
        //基底クラス内処理(フラグやミニゲーム用オブジェクトの初期化)
        base.Activate();

        //以下に必要な処理を追加

    }

    public override void Deactivate()
    {
        //基底クラス内処理(ミニゲーム用オブジェクトの終了処理)
        base.Deactivate();

        //以下に必要な処理を追加

    }
}
