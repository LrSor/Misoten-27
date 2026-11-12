using UnityEngine;

public class PointerImage : MonoBehaviour
{
    public Transform SphereTransfrom;
    private RectTransform rectTransform;
    // 任意の倍率、大きくすると小さい動きで大きく動く
    private float scale = 50.0f;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        float x = SphereTransfrom.position.x * scale;
        float y = SphereTransfrom.position.z * -scale;
        rectTransform.anchoredPosition = new Vector3(x, y, 0);
    }
}

