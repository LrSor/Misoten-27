using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    Rigidbody rb;

    void Start()
    {
        rb= GetComponent<Rigidbody>();
    }


    void Update()
    {
        float x=transform.position.x;
        float y=transform.position.y;
        float z=transform.position.z;
        if(Keyboard.current.wKey.isPressed)
        {
            transform.position = new Vector3(x,y,z+0.02f);
        }
        if (Keyboard.current.sKey.isPressed)
        {
            transform.position = new Vector3(x, y, z - 0.02f);
        }
        if (Keyboard.current.aKey.isPressed)
        {
            transform.position = new Vector3(x - 0.02f, y, z);
        }
        if (Keyboard.current.dKey.isPressed)
        {
            transform.position = new Vector3(x + 0.02f, y, z);
        }


    }
}
