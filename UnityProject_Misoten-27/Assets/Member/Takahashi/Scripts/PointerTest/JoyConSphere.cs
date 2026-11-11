using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyConSphere : MonoBehaviour
{
    public GameObject StartPoint;
    public GameObject Plane;

    // Update is called once per frame
    void Update()
    {
        var n = Plane.transform.up;
        var x = Plane.transform.position;
        var x0 = StartPoint.transform.position;
        var m = StartPoint.transform.forward;
        var h = Vector3.Dot(n, x);

        var intersectPoint = x0 + ((h - Vector3.Dot(n, x0)) / (Vector3.Dot(n, m))) * m;

        transform.position = intersectPoint;
    }
}
