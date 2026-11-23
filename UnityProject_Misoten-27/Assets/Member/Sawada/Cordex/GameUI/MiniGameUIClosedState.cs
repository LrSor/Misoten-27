public sealed class MiniGameUIClosedState : BaseState
{
    public override void InitState()
    {
        GetComponent<MiniGameUIBase>().EnterPhase(MiniGameUIBase.Phase.Closed);
    }
}
