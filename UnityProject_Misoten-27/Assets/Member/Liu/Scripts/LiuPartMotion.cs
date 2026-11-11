using UnityEngine;

[DisallowMultipleComponent]
public class LiuPartMotion : MonoBehaviour
{
    [Header("浮遊設定")]
    [SerializeField] private bool m_floatFlag = true;

    [Tooltip("上下に移動する幅（ローカル座標）")]
    [SerializeField, Min(0f)]
    private float m_floatHeight = 0.4f;

    [Tooltip("上下移動を1往復する時間（秒）")]
    [SerializeField, Min(0.1f)]
    private float m_floatPeriod = 2f;

    [Header("回転設定")]
    [SerializeField] private bool m_rotateFlag = true;

    [Tooltip("1秒あたりの回転角度")]
    [SerializeField] private float m_rotateSpeed = 45f;

    private Vector3 m_baseLocalPosition;
    private Quaternion m_baseLocalRotation;
    private float m_elapsedTime;

    private void Awake()
    {
        // モデルの初期位置と初期角度を保存する。
        m_baseLocalPosition = transform.localPosition;
        m_baseLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        m_elapsedTime += Time.deltaTime;

        // 初期位置を中心に、滑らかに上下移動する。
        float offset = 0f;

        if (m_floatFlag)
        {
            float period = Mathf.Max(0.1f, m_floatPeriod);
            float phase = m_elapsedTime * Mathf.PI * 2f / period;
            offset = Mathf.Sin(phase) * m_floatHeight;
        }

        transform.localPosition =
            m_baseLocalPosition + Vector3.up * offset;

        // モデル自身のY軸を中心に回転する。
        if (m_rotateFlag)
        {
            transform.Rotate(
                0f,
                m_rotateSpeed * Time.deltaTime,
                0f,
                Space.Self);
        }
    }

    private void OnDisable()
    {
        // 無効化されたら初期状態に戻す。
        transform.localPosition = m_baseLocalPosition;
        transform.localRotation = m_baseLocalRotation;
        m_elapsedTime = 0f;
    }
}
