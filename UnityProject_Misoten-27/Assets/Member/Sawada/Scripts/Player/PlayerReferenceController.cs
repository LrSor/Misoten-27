using UnityEngine;

public class PlayerReferenceController : MonoBehaviour
{
    [SerializeField] private PlayerScore m_playerScore;
    public PlayerScore PlayerScore => m_playerScore;
}
