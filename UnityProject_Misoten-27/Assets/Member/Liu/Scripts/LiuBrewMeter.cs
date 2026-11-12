using UnityEngine;
using UnityEngine.UI;

public class LiuBrewMeter : MonoBehaviour
{
    // 冲泡用のスライダー
    [SerializeField] private Slider brewSlider;

    // 指針の移動速度
    [SerializeField] private float pointerSpeed = 1.2f;

    private float elapsedTime;

    public bool IsBrewing { get; private set; }

    private void Awake()
    {
        // 開始するまでは非表示にする
        brewSlider.gameObject.SetActive(false);
    }

    private void Update()
    {
        // トランジション中は指針を動かさない。
        if (LIU_SCENE_CONTROLLER.IsTransitioning)
        {
            return;
        }

        if (!IsBrewing)
        {
            return;
        }

        // 指針を左右に往復させる
        elapsedTime += Time.deltaTime;
        brewSlider.value = Mathf.PingPong(
            elapsedTime * pointerSpeed, 1f);
    }

    public void BeginBrewing()
    {
        // 指針を左端から動かす
        elapsedTime = 0f;
        brewSlider.value = 0f;
        IsBrewing = true;
        brewSlider.gameObject.SetActive(true);
    }

    public int StopBrewing()
    {
        if (!IsBrewing)
        {
            return 0;
        }

        float position = brewSlider.value;

        IsBrewing = false;
        brewSlider.gameObject.SetActive(false);

        // 中央付近なら最高品質
        if (position >= 0.47f && position <= 0.53f)
        {
            return 2;
        }

        // 緑色の範囲なら成功
        if (position >= 0.4f && position <= 0.6f)
        {
            return 1;
        }

        // 範囲外なら失敗
        return 0;
    }
}
