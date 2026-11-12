using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class LiuPlayerMove : MonoBehaviour
{
    // プレイヤーの移動速度
    [SerializeField] private float moveSpeed = 3f;

    // 重力加速度
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        // キャラクターコントローラーを取得する
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // トランジション中は移動処理を行わない。
        if (LIU_SCENE_CONTROLLER.IsTransitioning)
        {
            return;
        }

        Vector3 direction = Vector3.zero;
        Keyboard keyboard = Keyboard.current;

        // WASDキーで移動方向を決める
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) direction.z += 1f;
            if (keyboard.sKey.isPressed) direction.z -= 1f;
            if (keyboard.aKey.isPressed) direction.x -= 1f;
            if (keyboard.dKey.isPressed) direction.x += 1f;
        }

        // 斜め移動でも速度が速くならないようにする
        direction = direction.normalized;

        // 接地中は下向きの速度を小さく保つ
        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        // 重力を加える
        verticalSpeed += gravity * Time.deltaTime;

        // 水平方向の移動と重力をまとめて適用する
        Vector3 velocity = direction * moveSpeed;
        velocity.y = verticalSpeed;

        controller.Move(velocity * Time.deltaTime);
    }
}
