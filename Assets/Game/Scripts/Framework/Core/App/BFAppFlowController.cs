namespace BF.Framework.Core.App
{
    public class BFAppFlowController
    {
        public BFAppFlowState CurrentState { get; private set; }

        public void EnterBoot()
        {
            CurrentState = BFAppFlowState.Boot;
        }
    }
}
