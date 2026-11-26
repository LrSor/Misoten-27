using UnityEngine;
using UnityEngine.UI;

public class TitleManager : BaseManager<TitleManager>
{
    [SerializeField] private Button m_buttonLoadGame;
    [SerializeField] private Button m_buttonRecord;

    void Start()
    {
        // ボタン有効確認 仮false
        if (m_buttonLoadGame != null) m_buttonLoadGame.interactable = false;
        if (m_buttonRecord != null) m_buttonRecord.interactable = false;
    }

    void Update()
    {
        
    }
}
