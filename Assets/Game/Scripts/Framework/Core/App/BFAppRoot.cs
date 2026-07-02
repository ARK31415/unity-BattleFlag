using BF.Framework.UI.Runtime;
using UnityEngine;

namespace BF.Framework.Core.App
{
    public class BFAppRoot : MonoBehaviour
    {
        public BFAppFlowController FlowController { get; private set; }

        [SerializeField] private BFUIRegistryConfig _uiRegistryConfig;
        [SerializeField] private BFUIRoot _uiRoot;

        public void Initialize()
        {
            var registry = new BFUIRegistry();
            if (_uiRegistryConfig != null)
            {
                registry.Register(_uiRegistryConfig);
            }
            var uiManager = new BFUIManager(registry, _uiRoot);

            FlowController = new BFAppFlowController();
            FlowController.EnterBoot();
        }
    }
}
