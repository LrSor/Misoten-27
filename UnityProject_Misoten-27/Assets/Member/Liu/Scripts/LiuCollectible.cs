using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LiuCollectible : MonoBehaviour
{
    // 上下に浮く高さと速さ
    [SerializeField] private float floatHeight = 0.15f;
    [SerializeField] private float floatSpeed = 2f;
    // 取得数を管理するオブジェクト
    [SerializeField] private LiuMiniGameManager gameManager;

    private Rigidbody body;
    private Vector3 startPosition;
    private bool isCollected;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPosition = body.position;
    }

    private void FixedUpdate()
    {
        // 最初の位置を中心に上下へ動かす
        float offset = Mathf.Sin(Time.fixedTime * floatSpeed) * floatHeight;
        body.MovePosition(startPosition + Vector3.up * offset);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 取得済みの場合は処理しない
        if (isCollected)
        {
            return;
        }

        // プレイヤー以外には反応しない
        if (other.GetComponentInParent<LiuPlayerMove>() == null)
        {
            return;
        }


        isCollected = true;

        // 取得数を1つ増やす
        gameManager.AddEnergy();

        // エネルギーを取得してオブジェクトを消す
        Debug.Log("エネルギーを取得！");
        Destroy(gameObject);
    }
}
