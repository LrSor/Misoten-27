using UnityEngine;

public abstract class BaseManager<T> : MonoBehaviour where T : BaseManager<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = (T)this;

        OnAwake();
    }
    protected virtual void OnAwake() { }

    protected void SetDontDestroyOnLoad()
    {
        transform.SetParent(null);
        DontDestroyOnLoad(this);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}