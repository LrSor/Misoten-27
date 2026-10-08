using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Axe : MiniGameObject
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joyconL;
    //private Joycon m_joyconR;
    private Joycon.Button? m_pressedButtonL;
    //private Joycon.Button? m_pressedButtonR;
    //============================================================

    [SerializeField] MiniGame m_miniGame;
    [SerializeField] GameObject m_gameClearObject;
#if UNITY_EDITOR
    private Vector3 m_shamAccel;
#endif


    public override void Activate()
    {
        //デバッグ用: キーボード操作が有効の場合、ジョイコンの初期化を停止
#if UNITY_EDITOR
        JoyconManager joyconManager = GameObject.FindFirstObjectByType<JoyconManager>();
        if (joyconManager.GetIsKeyboardPlay())
        {
            return;
        }
#endif

        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joyconL = m_joycons.Find(c => c.isLeft);
        //m_joyconR = m_joycons.Find(c => !c.isLeft);
        //============================================================
    }

    public override void Deactivate()
    {
        m_joyconL = null;

        transform.localEulerAngles = Vector3.zero;
    }

    public override void UpdateObject()
    {
#if UNITY_EDITOR
        bool hasJoycon = (m_joyconL != null);
        //ジョイコンが接続されている場合、キーボード操作処理を飛ばしジョイコン操作を行う
        if(!hasJoycon)
        {
            //--------------------------------------------
            // 疑似加速度（m_shamAccel）を Zキーで変化させる
            //--------------------------------------------
            if (Keyboard.current != null && Keyboard.current.zKey.isPressed)
            {
                // Zキー押しっぱなし → 徐々に振り下ろし方向へ（accel.y = -1 相当）
                m_shamAccel.y = Mathf.Clamp(m_shamAccel.y - Time.deltaTime * 1.2f, -2f, 0f);
            }
            else
            {
                // Zキー離す → 徐々に元の位置へ戻る（accel.y = 0 相当）
                m_shamAccel.y = Mathf.Clamp(m_shamAccel.y + Time.deltaTime * 1.1f, -1f, 0f);
            }

            // accel.y が 1.0 に近いほど「振り下ろし」と判定（元のロジックを流用）
            // ここでは Zキーで accel.y を擬似的に操作している

            //--------------------------------------------
            // 加速度に応じてオブジェクトの角度を補正
            //--------------------------------------------
            // y軸加速度が 1 の時 → z軸角度 -95°
            // 0 の時 → z軸角度 0°
            // 線形補間で滑らかに変化させる
            float TargetZ = Mathf.Lerp(0f, -95f, Mathf.Clamp01(-m_shamAccel.y));

            // 現在角度からスムーズに補正（好みで調整）
            float SmoothZ = Mathf.LerpAngle(transform.localEulerAngles.z, TargetZ, Time.deltaTime * 10f);

            transform.localEulerAngles = new Vector3(
                transform.localEulerAngles.x,
                transform.localEulerAngles.y,
                SmoothZ
            );

            return;
        }

#endif


        //--------------------------------------------
        // JoyConの加速度を取得
        //--------------------------------------------
        Vector3 accel = m_joyconL.GetAccel();
        // accel.y が 1.0 に近いほど「振り下ろし」と判定

        //--------------------------------------------
        // 加速度に応じてオブジェクトの角度を補正
        //--------------------------------------------
        // y軸加速度が 1 の時 → z軸角度 -95°
        // 0 の時 → z軸角度 0°
        // 線形補間で滑らかに変化させる
        float targetZ = Mathf.Lerp(0f, -95f, Mathf.Clamp01(-accel.y));

        // 現在角度からスムーズに補正（好みで調整）
        float smoothZ = Mathf.LerpAngle(transform.localEulerAngles.z, targetZ, Time.deltaTime * 10f);

        transform.localEulerAngles = new Vector3(
            transform.localEulerAngles.x,
            transform.localEulerAngles.y,
            smoothZ
        );
    }

    public void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject == m_gameClearObject)
        {
            //ミニゲームクリアフラグ
            m_miniGame.FinishMiniGame();

#if UNITY_EDITOR
            bool hasJoycon = (m_joyconL != null);
            if (!hasJoycon)
            {
                float collisionPower = Mathf.Abs(m_shamAccel.y);

                Debug.Log($"CollisionSpeed:{collisionPower}");

                //--------------------------------------------
                // スコアとしてミニゲームに渡す
                //--------------------------------------------
                //加速度×100を今回スコアとする
                float debugScore = collisionPower * 100;
                m_miniGame.SetScore((int)debugScore);

                //デバッグ用疑似加速度を初期化
                m_shamAccel = Vector3.zero;

                return;
            }
#endif

            //--------------------------------------------
            // JoyCon の加速度を取得（振り下ろし強さ）
            //--------------------------------------------
            Vector3 joyconAccel = m_joyconL.GetAccel();
            float joyconPower = Mathf.Abs(joyconAccel.y);

            {//--------------------------------------------
             // 衝突強度（物理的な衝突の強さ）=> 今回未使用
             //--------------------------------------------
             //float collisionPower = collision.relativeVelocity.magnitude;

                //--------------------------------------------
                // JoyCon × 衝突強度 を合わせた総合パワー　=> 今回未使用
                //--------------------------------------------
                // 例：両方を掛け合わせることで「強く振って強く当てた」ほど高得点
                //float totalPower = collisionPower * joyconPower;

                //Debug.Log($"JoyConPower:{joyconPower}, CollisionPower:{collisionPower}, Total:{totalPower}");
            }

            Debug.Log($"JoyConPower:{joyconPower}");

            //--------------------------------------------
            // スコアとしてミニゲームに渡す
            //--------------------------------------------
            //加速度 => 1～2辺りになる(強く振り下ろすほど大きい)
            //加速度×100を今回スコアとする
            float score = joyconPower * 100;
            m_miniGame.SetScore((int)score);
        }
    }
}
