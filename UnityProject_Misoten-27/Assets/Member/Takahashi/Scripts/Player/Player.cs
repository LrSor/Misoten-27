using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class Player : MonoBehaviour
{
    Rigidbody rb;

    [SerializeField] private float m_speed = 0.05f;


    [SerializeField] private Key m_keyUp;
    [SerializeField] private Key m_keyDown;
    [SerializeField] private Key m_keyRight;
    [SerializeField] private Key m_keyLeft;

    private KeyControl m_controlUp;
    private KeyControl m_controlDown;
    private KeyControl m_controlLeft;
    private KeyControl m_controlRight;

    [SerializeField] PlayerController m_controller;

    void Start()
    {
        rb= GetComponent<Rigidbody>();

        m_controlUp = Keyboard.current[m_keyUp];
        m_controlDown = Keyboard.current[m_keyDown];
        m_controlLeft = Keyboard.current[m_keyLeft];
        m_controlRight = Keyboard.current[m_keyRight];
    }


    void Update()
    {
        float x=transform.position.x;
        float y=transform.position.y;
        float z=transform.position.z;
        if(m_controlUp.isPressed)
        {
            transform.position = new Vector3(x,y,z+ m_speed);
        }
        if (m_controlDown.isPressed)
        {
            transform.position = new Vector3(x, y, z - m_speed);
        }
        if (m_controlLeft.isPressed)
        {
            transform.position = new Vector3(x - m_speed, y, z);
        }
        if (m_controlRight.isPressed)
        {
            transform.position = new Vector3(x + m_speed, y, z);
        }

        //配列0 -> x
        //配列1 -> y
        var stick = m_controller.PlayerJoycon.GetStick();
        if (!m_controller.PlayerJoycon.isLeft)
        {
            stick[0] = -stick[0];
            //stick[1] = -stick[1];
        }

        Vector3 move = Vector3.zero;
        move.x = stick[1] * 0.1f;
        move.z = stick[0] * 0.1f;

        transform.position += move;
    }
}
