using UnityEngine;
using System.Collections.Generic;

public class JoyConCube : MonoBehaviour
{
    private List<Joycon> joycons;

    // Values made available via Unity
    public float[] stick;
    public Vector3 gyro;
    public Vector3 accel;
    public int jc_ind = 0;
    public Quaternion orientation;

    void Start()
    {
        gyro = new Vector3(0, 0, 0);
        accel = new Vector3(0, 0, 0);
        joycons = JoyconManager.Instance.j;
        if (joycons.Count < jc_ind + 1)
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // make sure the Joycon only gets checked if attached
        if (joycons.Count > 0)
        {
            Joycon j = joycons[jc_ind];

            // Bボタンでセンター位置のリセット
            if (j.GetButtonDown(Joycon.Button.DPAD_DOWN))
            {
                j.Recenter();
            }

            gyro = j.GetGyro();
            accel = j.GetAccel();

            orientation = j.GetVector();
            gameObject.transform.rotation = orientation;
        }
    }
}
