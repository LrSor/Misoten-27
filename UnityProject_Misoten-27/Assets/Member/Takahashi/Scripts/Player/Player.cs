using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    Rigidbody rb;

    [SerializeField] private float m_speed = 0.05f;
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
            transform.position = new Vector3(x,y,z+ m_speed);
        }
        if (Keyboard.current.sKey.isPressed)
        {
            transform.position = new Vector3(x, y, z - m_speed);
        }
        if (Keyboard.current.aKey.isPressed)
        {
            transform.position = new Vector3(x - m_speed, y, z);
        }
        if (Keyboard.current.dKey.isPressed)
        {
            transform.position = new Vector3(x + m_speed, y, z);
        }


    }
}
