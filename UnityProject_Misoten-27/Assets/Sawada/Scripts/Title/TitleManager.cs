using UnityEngine;
using UnityEngine.UI;

public class TitleManager : BaseManager<TitleManager>
{
    [SerializeField] private Button m_buttonLoadGame;
    [SerializeField] private Button m_buttonRecord;

    void Start()
    {
        // ボタン有効確認 仮false
        m_buttonLoadGame.interactable = false;
        m_buttonRecord.interactable = false;
    }

    void Update()
    {
        
    }
}
