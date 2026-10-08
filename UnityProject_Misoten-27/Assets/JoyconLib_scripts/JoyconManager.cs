using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using System;
public class JoyconManager: MonoBehaviour
{

    // Settings accessible via Unity
    public bool EnableIMU = true;
    public bool EnableLocalize = true;

	// Different operating systems either do or don't like the trailing zero
	private const ushort vendor_id = 0x57e;
	private const ushort vendor_id_ = 0x057e;
	private const ushort product_l = 0x2006;
	private const ushort product_r = 0x2007;

    public List<Joycon> j; // Array of all connected Joy-Cons
    static JoyconManager instance;

#if UNITY_EDITOR
    [Header("デバッグ用キーボード操作(チェックでキーボード操作有効)")]
    [SerializeField] private bool m_isKeyboardPlay = false;
#endif

    public static JoyconManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        //キーボード操作有効の場合、JoyconManagerの停止
#if UNITY_EDITOR
        if (m_isKeyboardPlay)
        {
            Debug.Log("デバッグ用キーボード操作有効: JoyconManagerを停止");
            enabled = false;
            return;
        }
        else
        {
            Debug.Log("デバッグ用キーボード操作無効: JoyconManagerを実行");
        }
#endif
        if (instance != null) Destroy(gameObject);
        instance = this;
		int i = 0;

		j = new List<Joycon>();
		bool isLeft = false;
		HIDapi.hid_init();

		IntPtr ptr = HIDapi.hid_enumerate(vendor_id, 0x0);
		IntPtr top_ptr = ptr;

		if (ptr == IntPtr.Zero)
		{
			ptr = HIDapi.hid_enumerate(vendor_id_, 0x0);
			if (ptr == IntPtr.Zero)
			{ 
				HIDapi.hid_free_enumeration(ptr);
				Debug.Log ("No Joy-Cons found!");
			}
		}
		hid_device_info enumerate;
		while (ptr != IntPtr.Zero) {
			enumerate = (hid_device_info)Marshal.PtrToStructure (ptr, typeof(hid_device_info));

			Debug.Log (enumerate.product_id);
				if (enumerate.product_id == product_l || enumerate.product_id == product_r) {
					if (enumerate.product_id == product_l) {
						isLeft = true;
						Debug.Log ("Left Joy-Con connected.");
					} else if (enumerate.product_id == product_r) {
						isLeft = false;
						Debug.Log ("Right Joy-Con connected.");
					} else {
						Debug.Log ("Non Joy-Con input device skipped.");
					}
					IntPtr handle = HIDapi.hid_open_path (enumerate.path);
					HIDapi.hid_set_nonblocking (handle, 1);
					j.Add (new Joycon (handle, EnableIMU, EnableLocalize & EnableIMU, 0.05f, isLeft));
					++i;
				}
				ptr = enumerate.next;
			}
		HIDapi.hid_free_enumeration (top_ptr);
    }

    void Start()
    {
		for (int i = 0; i < j.Count; ++i)
		{
			Debug.Log (i);
			Joycon jc = j [i];
			byte LEDs = 0x0;
			LEDs |= (byte)(0x1 << i);
			jc.Attach (leds_: LEDs);
			jc.Begin ();
		}
    }

    void Update()
    {
		for (int i = 0; i < j.Count; ++i)
		{
			j[i].Update();
		}
    }

    void OnApplicationQuit()
    {
#if UNITY_EDITOR
        if (m_isKeyboardPlay)
            return;
#endif
        for (int i = 0; i < j.Count; ++i)
		{
			j[i].Detach ();
		}
    }

    //デバッグ用
#if UNITY_EDITOR
    public bool GetIsKeyboardPlay()
    {
        return m_isKeyboardPlay;
    }
#endif
}
