using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerScreenSelector :
    MonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private int playerIndex;

    public void OnPointerClick(
        PointerEventData eventData)
    {
        DevelopmentPlayerInputManager.Instance
            .SelectPlayer(playerIndex);
    }
}
