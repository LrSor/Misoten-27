using UnityEngine;

public class ColliderFieldArmor : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        Debug.Log($"衝突: {other.gameObject.name}, {other.gameObject.tag}");

        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Playerに当たった！");
        }
    }
}
