using UnityEngine;

namespace BF.Framework.Core.App
{
    public class BFAppRoot : MonoBehaviour
    {
        public BFAppFlowController FlowController { get; private set; }

        public void Initialize()
        {
            FlowController = new BFAppFlowController();
            FlowController.EnterBoot();
        }
    }
}
