using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LiuMiniGameManager : MonoBehaviour
{
    // 画面に表示するテキスト
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text timerText;

    // 必要な材料の数と制限時間
    [SerializeField] private int targetCount = 3;
    [SerializeField] private float timeLimit = 60f;

    // シーン内のオブジェクト
    [SerializeField] private Transform player;
    [SerializeField] private Transform counter;
    [SerializeField] private Transform customer;
    [SerializeField] private GameObject coffeeCup;

    // 操作できる距離
    [SerializeField] private float interactionDistance = 2f;

    private LiuBrewMeter brewMeter;
    private LiuPlayerMove playerMove;

    private int collectedCount;
    private int coffeeQuality;
    private int stars;
    private float remainingTime;

    private bool coffeeReady;
    private bool coffeeServed;
    private bool brewMissed;

    private void Start()
    {
        // 同じオブジェクトの抽出ゲージを取得する
        brewMeter = GetComponent<LiuBrewMeter>();
        playerMove = player.GetComponent<LiuPlayerMove>();

        coffeeCup.SetActive(false);
        remainingTime = timeLimit;

        timerText.color = Color.white;
        timerText.text = $"Time: {Mathf.CeilToInt(remainingTime)}";
    }

    public void AddEnergy()
    {
        // 終了後は材料を加算しない
        if (coffeeServed || remainingTime <= 0f)
        {
            return;
        }

        if (collectedCount < targetCount)
        {
            collectedCount++;
        }
    }

    private bool IsNear(Transform target)
    {
        // 水平方向の距離を調べる
        Vector3 difference = player.position - target.position;
        difference.y = 0f;

        return difference.magnitude <= interactionDistance;
    }

    private void Update()
    {
        // トランジション終了後に、時間計測と操作受付を開始する。
        if (LIU_SCENE_CONTROLLER.IsTransitioning)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        bool pressedE = keyboard != null
            && keyboard.eKey.wasPressedThisFrame;

        // クリア後は評価を表示する
        if (coffeeServed)
        {
            string starText = new string('*', stars);
            countText.text = $"CLEAR! Stars: {starText}\nEnter: Return";

            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene("LiuMainScene");
            }

            return;
        }

        // 抽出中も制限時間を減らす
        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        timerText.text = $"Time: {Mathf.CeilToInt(remainingTime)}";

        if (remainingTime <= 10f)
        {
            timerText.color = Color.red;
        }

        // 時間切れになったら抽出と移動を止める
        if (remainingTime <= 0f)
        {
            if (brewMeter.IsBrewing)
            {
                brewMeter.StopBrewing();
            }

            playerMove.enabled = false;
            countText.text = "Time up!\nR: Retry";

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene("LiuMiniGameScene");
            }

            return;
        }

        // 抽出中はEキーで指針を止める
        if (brewMeter.IsBrewing)
        {
            countText.text = "Aim for GREEN!\nE: Stop";

            if (pressedE)
            {
                coffeeQuality = brewMeter.StopBrewing();
                playerMove.enabled = true;

                if (coffeeQuality > 0)
                {
                    PrepareCoffee();
                }
                else
                {
                    brewMissed = true;
                    countText.text = "Miss!\nE: Try again";
                }
            }

            return;
        }

        // 完成したコーヒーをお客さんへ届ける
        if (coffeeReady)
        {
            if (IsNear(customer))
            {
                countText.text = "E: Serve coffee";

                if (pressedE)
                {
                    coffeeServed = true;
                    coffeeCup.SetActive(false);
                    playerMove.enabled = false;

                    // 品質と残り時間から評価を決める
                    stars = 1;

                    if (coffeeQuality == 2)
                    {
                        stars++;
                    }

                    if (remainingTime >= 20f)
                    {
                        stars++;
                    }
                }
            }
            else
            {
                string qualityText = coffeeQuality == 2
                    ? "Perfect coffee!"
                    : "Good coffee!";

                countText.text = qualityText
                    + "\nGo to the green customer";
            }

            return;
        }

        // 材料を集める
        if (collectedCount < targetCount)
        {
            countText.text = $"Ingredients: {collectedCount} / {targetCount}";
            return;
        }

        if (!IsNear(counter))
        {
            countText.text = "Go to the counter";
            return;
        }

        countText.text = brewMissed
            ? "Miss!\nE: Try again"
            : "E: Start brewing";

        // 抽出中はプレイヤーの移動を止める
        if (pressedE)
        {
            brewMissed = false;
            playerMove.enabled = false;
            brewMeter.BeginBrewing();
            countText.text = "Aim for GREEN!\nE: Stop";
        }
    }

    private void PrepareCoffee()
    {
        // 完成したカップをプレイヤーに持たせる
        coffeeReady = true;

        coffeeCup.transform.SetParent(player, true);
        coffeeCup.transform.localPosition = new Vector3(0.7f, 0f, 0.4f);
        coffeeCup.transform.localRotation = Quaternion.identity;
        coffeeCup.SetActive(true);

        countText.text = coffeeQuality == 2
            ? "Perfect coffee!\nGo to the green customer"
            : "Good coffee!\nGo to the green customer";
    }
}
