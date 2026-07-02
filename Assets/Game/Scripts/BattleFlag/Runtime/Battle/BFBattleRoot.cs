using UnityEngine;

namespace BF.Game.Runtime.Battle
{
    /// <summary>
    /// 战斗场景根节点，持有当前战斗上下文。
    /// 挂载于战斗场景的顶层 GameObject。
    /// </summary>
    public class BFBattleRoot : MonoBehaviour
    {
        /// <summary>
        /// 当前战斗上下文引用。
        /// </summary>
        public BFBattleContext Context { get; private set; }

        /// <summary>
        /// 初始化战斗根节点，绑定上下文。
        /// </summary>
        /// <param name="context">战斗上下文数据</param>
        public void Initialize(BFBattleContext context)
        {
            Context = context;
        }
    }
}
