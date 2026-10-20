using UnityEngine;
using UnityEngine.InputSystem;

public class testDeisplay : MonoBehaviour
{
    [SerializeField] Camera m_cam;
    [SerializeField] byte m_num;

    void Start()
    {
        
    }

    void Update()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            CameraDisplaySetter.Instance.SetDisplay(m_cam, m_num);
        }
    }
}
