using UnityEngine;

public class ColliderFieldArmor : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // 接続
            PlayerInventory playerInventory = other.gameObject.GetComponent<PlayerInventory>();

            // 通知
        }
    }
}
