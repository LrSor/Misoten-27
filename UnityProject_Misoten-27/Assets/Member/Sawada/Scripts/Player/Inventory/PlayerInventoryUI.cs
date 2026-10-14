using UnityEngine;


/*
 * Kumei    26/10/14
 * 
 * アーマーインベントリ UI制御
 */
public class PlayerInventoryUI : MonoBehaviour
{
    //private PlayerInventory m_playerInventory;

    [SerializeField] private GameObject m_front1;
    [SerializeField] private GameObject m_front2;

    private void Start()
    {
        //m_playerInventory = GetComponent<PlayerInventory>();
    }

    public void testKirikae()
    {
        bool old = m_front1.activeSelf;
        m_front1.SetActive(!old);
        m_front2.SetActive(old);
    }
}
