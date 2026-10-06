using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    [Header("名前")]
    [SerializeField] string m_stateMachineName;

    readonly Dictionary<Type, BaseState> m_stateDictionary = new();
    
    [Header("現在のState")]
    [SerializeField] BaseState　m_currentState;

    void Awake()
    {
        // 型からStateを取得できるように登録
        List<BaseState> m_states = new List<BaseState>(GetComponents<BaseState>());
        foreach (BaseState state in m_states)
        {
            m_stateDictionary.Add(state.GetType(), state);
        }
    }

    void Start()
    {
        m_currentState = m_stateDictionary.Values.First();
        m_currentState.InitState();
    }

    void Update()
    {
        m_currentState?.UpdateState();
    }

    void FixedUpdate()
    {
        m_currentState?.FixedUpdateState();
    }

    // Stateの切り替え
    public void ChangeState<T>() where T : BaseState
    {
        if (!m_stateDictionary.TryGetValue(typeof(T), out BaseState nextState))
        {
            Debug.LogError($"{m_stateMachineName}: {typeof(T).Name} が登録されていません");
            return;
        }

        if (m_currentState == nextState) return;

        m_currentState?.UnInitState();
        m_currentState = nextState;
        m_currentState.InitState();

        Debug.Log($"{m_stateMachineName}: {typeof(T).Name}");
    }
}
