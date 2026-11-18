using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BaseArmor", menuName = "Scriptable Objects/BaseArmor")]
public class BaseArmorSO : ScriptableObject
{
    [Header("モデル")]
    public GameObject m_KARI_icon;
    public List<GameObject> m_temporaryOutfit;

    [Header("服に反映する色合い")]
    public Color m_color01 = Color.white;
    public Color m_color02 = Color.white;


    [Header("性能")]
    public float m_areaA = 0f;
    public float m_areaB = 0f;
    public float m_areaC = 0f;

    [Header("特殊性能 (仮)")]
    public float m_jump = 0f;
    public float m_moveSpeed = 0f;
}
