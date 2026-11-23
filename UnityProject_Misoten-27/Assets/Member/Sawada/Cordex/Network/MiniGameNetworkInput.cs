using System;
using UnityEngine;

/// <summary>One participant's device sample. Scores and elapsed time are never accepted from clients.</summary>
[Serializable]
public struct MiniGameNetworkInput
{
    public Vector3 m_acceleration, m_gyro;
    public Quaternion m_orientation;
    public bool m_isLeft, m_button, m_release;

    public static MiniGameNetworkInput Neutral => new MiniGameNetworkInput
    { m_isLeft = true, m_orientation = Quaternion.identity };

    public bool IsValid()
    {
        bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        bool Vector(Vector3 value, float limit) => Finite(value.x) && Finite(value.y) && Finite(value.z)
            && Mathf.Abs(value.x) <= limit && Mathf.Abs(value.y) <= limit && Mathf.Abs(value.z) <= limit;
        float magnitude = m_orientation.x * m_orientation.x + m_orientation.y * m_orientation.y
            + m_orientation.z * m_orientation.z + m_orientation.w * m_orientation.w;
        return Vector(m_acceleration, 16f) && Vector(m_gyro, 64f) && Finite(magnitude)
            && magnitude >= .9f && magnitude <= 1.1f;
    }
}
