using UnityEngine;

public abstract class BaseState  : MonoBehaviour
{
    public virtual void InitState() { }
    public virtual void UnInitState() { }
    public virtual void UpdateState() { }
    public virtual void FixedUpdateState() { }
}
