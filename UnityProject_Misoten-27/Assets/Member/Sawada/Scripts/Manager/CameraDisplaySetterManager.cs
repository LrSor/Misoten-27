using UnityEngine;

public class CameraDisplaySetter : BaseManager<CameraDisplaySetter>
{
    // 指定カメラを指定番号のディスプレイに割り振る
    public void SetDisplay(Camera camera, byte displayNumber)
    {
        if (camera == null) return;
        Debug.Log(displayNumber);
        camera.targetDisplay = displayNumber;
    }
}
