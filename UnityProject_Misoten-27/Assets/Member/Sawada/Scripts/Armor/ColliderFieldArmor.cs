using UnityEngine;

public class ColliderFieldArmor : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("PlayerSwapCollider"))
        {
            // 接続
            PlayerSwapController playerSwapController = other.gameObject.GetComponent<PlayerSwapController>();

            // 追加
            playerSwapController.AddCanSwapArmor(GetComponent<FieldArmorMachine>());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("PlayerSwapCollider"))
        {
            // 接続
            PlayerSwapController playerSwapController = other.gameObject.GetComponent<PlayerSwapController>();

            // 削除
            playerSwapController.RemoveCanSwapArmor(GetComponent<FieldArmorMachine>());
        }
    }
}
