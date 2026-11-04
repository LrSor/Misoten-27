using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    //============================================================
    // JoyCon関連
    //============================================================
    private static readonly Joycon.Button[] m_buttons =
        Enum.GetValues(typeof(Joycon.Button)) as Joycon.Button[];

    private List<Joycon> m_joycons;
    private Joycon m_joycon;
    public Joycon PlayerJoycon => m_joycon;
    //============================================================



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //============================================================
        // JoyCon初期化
        //============================================================
        m_joycons = JoyconManager.Instance.j;

        if (m_joycons == null || m_joycons.Count <= 0) return;

        m_joycon = m_joycons.First();
        //============================================================
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
