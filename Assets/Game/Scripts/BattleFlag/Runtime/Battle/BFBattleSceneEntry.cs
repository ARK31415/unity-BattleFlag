using System;
using UnityEngine;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗场景入口，挂载在战斗场景的入口 GameObject 上。
    /// 负责战斗场景的初始化流程，完成后通知场景就绪。
    /// </summary>
    public class BFBattleSceneEntry : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }

        [SerializeField] private BFBattleRoot _battleRoot;

        public BFBattleContext Context { get; private set; }

        public event Action OnReady;

        private void Awake()
        {
            if (!IsInitialized)
            {
                InitializeScene();
            }
        }

        public void InitializeScene(BFBattleContext context = null)
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[BFBattleSceneEntry] 场景已初始化，跳过重复调用。");
                return;
            }

            Context = context ?? new BFBattleContext();

            if (_battleRoot == null)
            {
                _battleRoot = GetComponent<BFBattleRoot>();
                if (_battleRoot == null)
                {
                    _battleRoot = FindAnyObjectByType<BFBattleRoot>();
                }
            }

            if (_battleRoot != null)
            {
                _battleRoot.Initialize(Context);
            }
            else
            {
                Debug.LogError("[BFBattleSceneEntry] 场景中未找到 BFBattleRoot！");
            }

            IsInitialized = true;
            Debug.Log($"[BFBattleSceneEntry] 战斗场景初始化完成: {Context.BattleId}");

            NotifyReady();
        }

        public void NotifyReady()
        {
            Debug.Log("[BFBattleSceneEntry] 战斗场景就绪通知。");
            OnReady?.Invoke();
        }
    }
}
