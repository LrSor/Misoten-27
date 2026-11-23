using System;
using UnityEngine;

/// <summary>One participant's device sample. Scores and elapsed time are never accepted from clients.</summary>
[Serializable]
public struct MiniGameNetworkInput
{
    public Vector3 acceleration, gyro;
    public Quaternion orientation;
    public bool isLeft, button, release;

    public static MiniGameNetworkInput Neutral => new MiniGameNetworkInput
    { isLeft = true, orientation = Quaternion.identity };

    public bool IsValid()
    {
        bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        bool Vector(Vector3 value, float limit) => Finite(value.x) && Finite(value.y) && Finite(value.z)
            && Mathf.Abs(value.x) <= limit && Mathf.Abs(value.y) <= limit && Mathf.Abs(value.z) <= limit;
        float magnitude = orientation.x * orientation.x + orientation.y * orientation.y
            + orientation.z * orientation.z + orientation.w * orientation.w;
        return Vector(acceleration, 16f) && Vector(gyro, 64f) && Finite(magnitude)
            && magnitude >= .9f && magnitude <= 1.1f;
    }
}
