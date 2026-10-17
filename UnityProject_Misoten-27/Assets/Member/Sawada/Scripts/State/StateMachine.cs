using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    [Header("名前")]
    [SerializeField] string m_stateMachineName;

    readonly Dictionary<Type, BaseState> m_stateDictionary = new();
    
    [Header("State")]
    [SerializeField] String m_initialState;
    [SerializeField] BaseState　m_currentState;      // 見るためのSerializeField

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
        // 初期Stateを設定
        BaseState initialState = m_stateDictionary
            .Where(pair => pair.Key.Name == m_initialState)
            .Select(pair => pair.Value)
            .FirstOrDefault();
        if (initialState != null)
        {
            m_currentState = initialState;
        }
        else
        {
            m_currentState = m_stateDictionary.Values.First();
        }
        m_currentState?.InitState();
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
        m_currentState?.InitState();

        Debug.Log($"{m_stateMachineName}: {typeof(T).Name}");
    }
}
