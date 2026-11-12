using UnityEngine;

public class CharacterReferenceController : MonoBehaviour
{
    [SerializeField] private Inventory m_inventory;
    public Inventory Inventory => m_inventory;
}
