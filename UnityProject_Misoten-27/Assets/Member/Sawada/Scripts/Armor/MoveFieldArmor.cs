using UnityEngine;

public class MoveFieldArmor : MonoBehaviour
{
    Vector3 m_initialPosition;

    private void Start()
    {
        m_initialPosition = transform.position;
    }

    private void FixedUpdate()
    {
        Floating();
    }

    // 上下プカプカ
    private void Floating()
    {
        float y = (Mathf.Sin(Time.time) + 1.0f) * 0.5f;
        transform.position = new Vector3(m_initialPosition.x, m_initialPosition.y + y, m_initialPosition.z);
    }
}
